using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DeathrunManager.Config;
using DeathrunManager.Interfaces.Managers.PlayersManager;
using DeathrunManager.Shared;
using DeathrunManager.Shared.Objects;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.Listeners;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;

namespace DeathrunManager.Managers.PlayersManager;

internal class PlayersManager(
    ILogger<PlayersManager> logger,
    ISharedSystem sharedSystem) : IPlayersManager, IDeathrunManager, IClientListener
{
    public static PlayersManager Instance = null!;
    
    private static readonly ConcurrentDictionary<ulong, DeathrunPlayer> DeathrunPlayers = new();
    
    #region IModule
    
    public bool Init()
    {
        Instance = this;
        
        logger.LogInformation("[Deathrun][PlayersManager] {colorMessage}", "Load Players Manager");
        
        sharedSystem.GetModSharp().InstallGameFrameHook(null, OnGameFramePost);
        sharedSystem.GetClientManager().InstallClientListener(this);
        
        sharedSystem.GetClientManager().InstallCommandCallback("kill", OnClientKillCommand);
        
        return true;
    }

    public static void OnPostInit() { }

    public void Shutdown()
    {
        ClearDeathrunPlayers();
        
        sharedSystem.GetModSharp().RemoveGameFrameHook(null, OnGameFramePost);
        sharedSystem.GetClientManager().RemoveClientListener(this);
        
        sharedSystem.GetClientManager().RemoveCommandCallback("kill", OnClientKillCommand);
        
        logger.LogInformation("[Deathrun][PlayersManager] {colorMessage}", "Unload Players Manager");
    }

    #endregion

    #region Hooks

    private void OnGameFramePost(bool simulating, bool bFirstTick, bool bLastTick)
    {
        //logger.LogInformation("[MS-ZP] OnGameFramePre");
        foreach (var deathrunPlayer in GetAllValidDeathrunPlayers().ToList())
        {
            deathrunPlayer.PlayerThink();
        }
    }

    #endregion
    
    #region Listeners
    
    public void OnClientConnected(IGameClient client)
    {
        if (client?.IsValid is not true) return;

        //skip if we couldn't add the client to the deathrun players dictionary
        DeathrunPlayers.TryAdd(client.SteamId != 0 ? client.SteamId : client.Slot,
                                                          new DeathrunPlayer(client));
        
        var deathrunPlayer = DeathrunPlayers.GetValueOrDefault(client.SteamId != 0 ? client.SteamId : client.Slot);
        if (deathrunPlayer?.LivesSystem is null) return;
        
        //skip getting data from the database if we've set the SaveLivesToDatabase to false
        if (LivesSystemManager.LivesSystemManager.LivesSystemConfig?.SaveLivesToDatabase is not true)
        {
            deathrunPlayer.LivesSystem?.SetLivesNum(LivesSystemManager.LivesSystemManager.LivesSystemConfig?.StartLivesNum ?? 1);
            return;
        }
        else
        {
            Task.Run(async () =>
            {
                ulong steamId64 = deathrunPlayer.Client.SteamId;
                var livesNumFromDb = await PlayersManager.GetSavedLives(steamId64);
                
                deathrunPlayer.LivesSystem?.SetLivesNum(livesNumFromDb);
            });
        }
    }
    
    public void OnClientPutInServer(IGameClient client)
    {
        if (client?.IsValid is not true) return;

        DeathrunPlayers.TryAdd(client.SteamId != 0 ? client.SteamId : client.Slot, new DeathrunPlayer(client));
    }
    
    public void OnClientDisconnected(IGameClient client, NetworkDisconnectionReason reason)
    {
        if (client?.IsValid is not true) return;

        if (DeathrunPlayers.TryRemove(client.SteamId != 0 ? client.SteamId : client.Slot, out var removedDeathrunPlayer) is true)
        {
            //skip bots here
            if (client.SteamId == 0) return;
            
            //check if the lives system is enabled and we are saving the lives to the database
            if (LivesSystemManager.LivesSystemManager.LivesSystemConfig?.EnableLivesSystem is true
                && LivesSystemManager.LivesSystemManager.LivesSystemConfig.SaveLivesToDatabase is true)
            {
                if (removedDeathrunPlayer.LivesSystem is null) return;
                
                ulong steamId64 = removedDeathrunPlayer.Client.SteamId;
                var livesNum = removedDeathrunPlayer.LivesSystem.GetLivesNum;
                
                Task.Run(() => SaveLivesToDatabase(steamId64, livesNum));
            }
        }
    }
    
    #endregion

    #region Commands
    
    private ECommandAction OnClientKillCommand(IGameClient client, StringCommand command)
    {
        var deathrunPlayer = GetDeathrunPlayer(client);
        if (deathrunPlayer is null) return ECommandAction.Stopped;

        if (deathrunPlayer.IsValidAndAlive is not true) return ECommandAction.Stopped;
        
        if (DeathrunManagerConfig.Config.EnableKillCommandForCTs is true
            && deathrunPlayer.Controller?.Team is CStrikeTeam.CT)
        {
            deathrunPlayer.PlayerPawn?.Slay();
        }
        
        if (DeathrunManagerConfig.Config.EnableKillCommandForTs is true
            && deathrunPlayer.Controller?.Team is CStrikeTeam.TE)
        {
            deathrunPlayer.PlayerPawn?.Slay();
        }
        
        return ECommandAction.Stopped;
    }
    
    #endregion
    
    #region Deathrun Players
    
    private static void ClearDeathrunPlayers() => DeathrunPlayers.Clear();
    
    #endregion
    
    #region DeathrunPlayerAsync

    private static async Task SaveLivesToDatabase(ulong steamId64, int newLivesNum)
    {
        try
        {
            await using var connection = new MySqlConnection(LivesSystemManager.LivesSystemManager.ConnectionString);
            await connection.OpenAsync();
            
            var insertUpdateLivesQuery 
                = $@" INSERT INTO `{(LivesSystemManager.LivesSystemManager.LivesSystemConfig?.TableName ?? "deathrun_players")}` 
                      ( steamid64, `lives` )  
                      VALUES 
                      ( @SteamId64, @NewLives ) 
                      ON DUPLICATE KEY UPDATE 
                                       `lives`  = '{newLivesNum}'
                    ";
    
            await connection.ExecuteAsync(insertUpdateLivesQuery,
                new {
                            SteamId64        = steamId64, 
                            NewLives         = newLivesNum
                          });
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        
    }
    
    public static async Task<int> GetSavedLives(ulong steamId64)
    {
        try
        {
            await using var connection = new MySqlConnection(LivesSystemManager.LivesSystemManager.ConnectionString);
            await connection.OpenAsync();
    
            //fast check if the player has saved lives data
            var hasSavedLivesData = await HasSavedLivesData(steamId64);
            if (hasSavedLivesData is not true) return 0;
            
            //take the lives num from the database
            var livesNum = await connection.QueryFirstOrDefaultAsync<int>
            ($@"SELECT
                       `lives`
                    FROM `{(LivesSystemManager.LivesSystemManager.LivesSystemConfig?.TableName ?? "deathrun_players")}`
                    WHERE steamid64 = @SteamId64
                 ",
                new { SteamId64 = steamId64 }
            
            );
            
            return livesNum;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        
        return 0;
    }

    private static async Task<bool> HasSavedLivesData(ulong steamId64)
    {
        try
        {
            await using var connection = new MySqlConnection(LivesSystemManager.LivesSystemManager.ConnectionString);
            await connection.OpenAsync();
    
            var hasSavedLivesData 
                = await connection.QueryFirstOrDefaultAsync<bool>
                                    ($@"SELECT EXISTS(SELECT 1 FROM `{(LivesSystemManager.LivesSystemManager.LivesSystemConfig?.TableName ?? "deathrun_players")}`
                                            WHERE steamid64 = @SteamId64 LIMIT 1)
                                         ",
                                        new { SteamId64 = steamId64 }
                                    
                                    );
            
            return hasSavedLivesData;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        
        return false;
    }

    #endregion
    
    #region DeathrunPlayer Shared

    public IDeathrunPlayer? GetDeathrunPlayer(IGameClient client)
    {
        if (client?.IsValid is not true) return null;
        
        var deathrunPlayer = DeathrunPlayers.GetValueOrDefault(client.SteamId != 0 ? client.SteamId : client.Slot);
        return deathrunPlayer?.IsValid is not true ? null : deathrunPlayer;
    }
    
    public IReadOnlyCollection<IDeathrunPlayer> GetAllValidDeathrunPlayers() 
        => DeathrunPlayers.Values
            .Where(deathrunPlayer => deathrunPlayer.IsValid is true)
            .ToList();

    public IReadOnlyCollection<IDeathrunPlayer> GetAllAliveDeathrunPlayers() 
        => DeathrunPlayers.Values
            .Where(deathrunPlayer => deathrunPlayer.PlayerPawn?.IsAlive is true)
            .ToList();
    
    #endregion
    
    int IClientListener.ListenerVersion => IClientListener.ApiVersion;
    int IClientListener.ListenerPriority => 8;
}




