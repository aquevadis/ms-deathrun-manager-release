using System;
using System.Linq;
using DeathrunManager.Config;
using DeathrunManager.Interfaces.Managers.GameplayManager;
using DeathrunManager.Interfaces.Managers.Native;
using DeathrunManager.Managers.PlayersManager;
using DeathrunManager.Shared;
using DeathrunManager.Shared.Enums;
using DeathrunManager.Shared.Objects;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.HookParams;
using Sharp.Shared.Listeners;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;

namespace DeathrunManager.Managers.GameplayManager;

internal class GameplayManager(
    ILogger<GameplayManager> logger,
    ISharedSystem sharedSystem,
    IEventManager eventManager) : IGameplayManager, IDeathrunManager, IClientListener, IGameListener, IEntityListener
{
    public static GameplayManager Instance = null!;
    
    private DRoundState _deathrunRoundState = DRoundState.Unset;
    private void SetRoundState(DRoundState newState) { _deathrunRoundState = newState; }
    public DRoundState GetRoundState() => _deathrunRoundState;
    
    private DeathrunPlayer? _gameMasterDeathrunPlayer = null;
    private void SetGameMaster(DeathrunPlayer? gameMaster) { _gameMasterDeathrunPlayer = gameMaster; }
    public IDeathrunPlayer? GetGameMaster() => _gameMasterDeathrunPlayer;
    
    // ReSharper disable once MemberCanBePrivate.Global
    public static IGameRules GameRules = null!;
    
    private IConVar? _autoBunnyHopCvar  = null;
    
    #region IModule
    
    public bool Init()
    {
        Instance = this;

        if (DeathrunManagerConfig.Config.EnableAutoBunnyHopping is true)
        {
            _autoBunnyHopCvar = sharedSystem.GetConVarManager().FindConVar("sv_autobunnyhopping");
            if (_autoBunnyHopCvar is not null) _autoBunnyHopCvar.Flags &= ~ConVarFlags.Replicated;
            
            sharedSystem.GetHookManager().PlayerRunCommand.InstallHookPre(OnPlayerRunCommandPre);
        }
        
        sharedSystem.GetHookManager().PlayerSpawnPost.InstallForward(PlayerSpawnPost);
        sharedSystem.GetHookManager().HandleCommandJoinTeam.InstallHookPre(CommandJoinTeamPre);
        
        sharedSystem.GetClientManager().InstallClientListener(this);
        sharedSystem.GetModSharp().InstallGameListener(this);
        sharedSystem.GetEntityManager().InstallEntityListener(this);
        
        eventManager.HookEvent("round_start", OnRoundStart);
        eventManager.HookEvent("round_end", OnRoundEnd);
        
        logger.LogInformation("[Deathrun][GameplayManager] {colorMessage}", "Load Gameplay Manager");
        
        return true;
    }

    public static void OnPostInit() { }

    public void Shutdown()
    {
        sharedSystem.GetHookManager().PlayerSpawnPost.RemoveForward(PlayerSpawnPost);
        sharedSystem.GetHookManager().HandleCommandJoinTeam.RemoveHookPre(CommandJoinTeamPre);
        
        if (DeathrunManagerConfig.Config.EnableAutoBunnyHopping is true)
        {
            sharedSystem.GetHookManager().PlayerRunCommand.RemoveHookPre(OnPlayerRunCommandPre);
        }
        
        sharedSystem.GetClientManager().RemoveClientListener(this);
        sharedSystem.GetModSharp().RemoveGameListener(this);
        sharedSystem.GetEntityManager().RemoveEntityListener(this);
        
        logger.LogInformation("[Deathrun][GameplayManager] {colorMessage}", "Unload Gameplay Manager");
    }

    #endregion

    #region Hooks
    
    private HookReturnValue<EmptyHookReturn> OnPlayerRunCommandPre(IPlayerRunCommandHookParams parms, HookReturnValue<EmptyHookReturn> returnValue)
    {
        var client = parms.Client;
        if (client?.IsValid is not true) return new();
        
        _autoBunnyHopCvar?.ReplicateToClient(client, "1");
        _autoBunnyHopCvar?.Set(1);
        
        return new();
    }
    
    private static HookReturnValue<bool> CommandJoinTeamPre(IHandleCommandJoinTeamHookParams parms, HookReturnValue<bool> result)
    {
        var deathrunPlayer = PlayersManager.PlayersManager.Instance.GetDeathrunPlayer(parms.Client);
        if (deathrunPlayer is null) return new ();
        
        //allow the player to join the spectators or CT team freely
        if (parms.Team is (int) CStrikeTeam.Spectator or (int) CStrikeTeam.CT) return new ();
    
        //if the player didn't click on any team option, auto-assign to CT
        if (parms.Team is (int)CStrikeTeam.UnAssigned) deathrunPlayer.Controller?.SwitchTeam(CStrikeTeam.CT);
        
        //block any other team change option/s
        return new (EHookAction.SkipCallReturnOverride);
    }
    
    private void PlayerSpawnPost(IPlayerSpawnForwardParams parms)
    {
        var deathrunPlayer = PlayersManager.PlayersManager.Instance.GetDeathrunPlayer(parms.Client);
        if (deathrunPlayer is null) return;
        
        if (deathrunPlayer != GetGameMaster())
            deathrunPlayer.ChangeClass(DPlayerClass.Contestant);
    }

    #endregion
    
    #region Listeners
    
    //Game Listeners
    
    public void OnGameInit() => GameRules = DeathrunManager.Bridge.ModSharp.GetGameRules();

    /// <summary>
    /// Activates the gameplay settings and configurations for the deathrun game mode.
    /// This method executes server commands to adjust gameplay rules, including team limits, default weapons, bot behaviors,
    /// movement acceleration, and other gameplay-related configurations. It also applies additional settings
    /// such as removing money from the game, adjusting round time, and enabling semi-clipping through teammates based on the configuration.
    /// </summary>
    public void OnGameActivate()
    {
        logger.LogInformation("[Deathrun][GameplayManager] {colorMessage}", "Execute game mode cvars and settings!");
        
        sharedSystem.GetModSharp().PushTimer(() =>
        {
            //execute cvars crucial for running the deathrun game mode 1/2
            sharedSystem.GetModSharp().ServerCommand
            (
                // "ms_block_valve_log 1;"// + 
                "mp_limitteams 0;"
                + "mp_autoteambalance false;"
                + "bot_join_team ct;"
                + "mp_ct_default_melee weapon_knife;"
                + $"mp_ct_default_secondary weapon_usp_silencer;"
                + $"mp_ct_default_primary ;"
            );
            //execute cvars crucial for running the deathrun game mode 2/2
            sharedSystem.GetModSharp().ServerCommand 
            (     
                  "mp_t_default_melee weapon_knife;"
                  + $"mp_t_default_secondary\" \";"
                  + $"mp_t_default_primary\" \";"
                  + "bot_quota_mode fill;"
                  + "sv_enablebunnyhopping 1;"
                  + "sv_airaccelerate 99999"
            );

            //remove money from the game and hud
            if (DeathrunManagerConfig.Config.RemoveMoneyFromGameAndHud is true)
            {
                //remove money from the game and hud 1/5
                sharedSystem.GetModSharp()
                    .ServerCommand
                    (
                        "cash_player_bomb_defused 0;"
                        + "cash_player_bomb_planted 0;"
                        + "cash_player_damage_hostage -30;"
                        + "cash_player_interact_with_hostage 0;"
                        + "cash_player_killed_enemy_default 0;"
                        + "cash_team_win_by_time_running_out_bomb 0;"
                    );
                //remove money from the game and hud 2/5
                sharedSystem.GetModSharp()
                    .ServerCommand
                    (
                        "cash_player_killed_enemy_factor 0;"
                        + "cash_player_killed_hostage -1000;"
                        + "cash_player_killed_teammate -300;"
                        + "cash_player_rescued_hostage 0;"
                        + "cash_team_elimination_bomb_map 0;"
                        + "cash_team_elimination_hostage_map_t 0;"
                    );
                //remove money from the game and hud 3/5
                sharedSystem.GetModSharp()
                    .ServerCommand
                    (
                        "cash_team_elimination_hostage_map_ct 0;"
                        + "cash_team_hostage_alive 0;"
                        + "cash_team_hostage_interaction 0;"
                        + "cash_team_loser_bonus 0;"
                        + "cash_team_bonus_shorthanded 0;"
                        + "cash_team_loser_bonus_consecutive_rounds 0;"
                    );
                //remove money from the game and hud 4/5
                sharedSystem.GetModSharp()
                    .ServerCommand
                    (
                        "cash_team_planted_bomb_but_defused 0;"
                        + "cash_team_rescued_hostage 0;"
                        + "cash_team_terrorist_win_bomb 0;"
                        + "cash_team_win_by_defusing_bomb 0;"
                        + "cash_team_win_by_hostage_rescue 0;"
                        + "cash_team_win_by_time_running_out_hostage 0;"
                    );
                //remove money from the game and hud 5/5
                sharedSystem.GetModSharp()
                    .ServerCommand
                    (
                         "mp_playercashawards 0;"
                         + "mp_teamcashawards 0;"
                         + "mp_startmoney 0;"
                         + "mp_maxmoney 0;"
                         + "mp_afterroundmoney 0;"
                         + "mp_autokick 0;"
                    );
                
                //remove money from the game and hud 6/6
                sharedSystem.GetModSharp()
                    .ServerCommand
                    (
                        "sv_maxspeed 9999;"
                        + "sv_alltalk 0;"
                        + "sv_stopspeed 0;"
                        + "sv_backspeed 0.1;"
                        + "sv_accelerate 10;"
                        + "sv_wateraccelerate 10;"
                        + "sv_accelerate_use_weapon_speed false;"
                    );
            }

            //adjust round time to 1 hour
            if (DeathrunManagerConfig.Config.SetRoundTimeOneHour is true)
            {
                sharedSystem.GetModSharp().ServerCommand
                (
                    "mp_roundtime 60;"
                    + "mp_roundtime_defuse 60;"
                    + "mp_roundtime_hostage 60;"
                );
            }

            //set semi-clipping to true for teammates if enabled in config
            if (DeathrunManagerConfig.Config.EnableClippingThroughTeamMates is true)
            {
                sharedSystem.GetModSharp().ServerCommand
                (
                    "mp_solid_teammates 2;"
                );
            }
        }, 5f);
    }
    
    //Client Listeners
    
    public void OnClientDisconnected(IGameClient client, NetworkDisconnectionReason reason)
    {
        if (client?.IsValid is not true) return;
        
        var deathrunPlayer = PlayersManager.PlayersManager.Instance.GetDeathrunPlayer(client);
        if (deathrunPlayer is null) return;

        if (deathrunPlayer.Class is DPlayerClass.GameMaster)
        {
            GameRules.TerminateRound(3, RoundEndReason.CTsWin);
        }
    }
    
    //Entity Listeners
    
    public void OnEntitySpawned(IBaseEntity entity)
    {
        if (entity.IsValidEntity is not true) return;
        
        //skip if RemoveBuyZones is disabled in the config
        if (DeathrunManagerConfig.Config.RemoveBuyZones is not true) return;
        
        if (entity.Classname.Contains("buyzone", StringComparison.OrdinalIgnoreCase))
        {
            sharedSystem.GetModSharp().InvokeFrameAction(() =>
            {
                if (entity?.IsValidEntity is not true) return;
                entity.AcceptInput("Kill");
            });
        }
    }
    
    #endregion
    
    #region Events
    
    private HookReturnValue<bool> OnRoundStart(EventHookParams evParms)
    {
        StartDeathrunRound();
        return new ();
    }

    private HookReturnValue<bool> OnRoundEnd(EventHookParams evParms)
    {
        EndDeathrunRound();
        return new ();
    }

    #endregion

    #region Deathrun Round

    private void StartDeathrunRound()
    {
        SetRoundState(DRoundState.StartPre);
        
        //skip if we are in a warmup period;
        if (GameRules.IsWarmupPeriod is true)
        {
            logger.LogInformation("[GameplayManager][OnRoundStart] {colorMessage}", "Game mode stopped during warmup period!");
            return;
        }
        
        SetRoundState(DRoundState.CheckGameModeRequirements);
        
        //check if we have enough players to start the deathrun;
        //we need at least 2 players to start the deathrun - one Contestant(CT) and one GameMaster(T)
        if (PlayersManager.PlayersManager.Instance.GetAllValidDeathrunPlayers().Count < 2)
        {
            //ingame-msg: "Not enough players to start the deathrun"
            return;
        }
        
        //delay the picking of the game master by one tick
        sharedSystem.GetModSharp().PushTimer(() =>
        {
            SetRoundState(DRoundState.PickingGameMaster);

            //return if pick game master failed
            if (PickGameMaster() is not true)
            {
                logger.LogInformation("[GameplayManager][OnRoundStart] {colorMessage}", "Failed picking game master!");
                GameRules.TerminateRound(2, RoundEndReason.RoundDraw);
                return;
            }
        
            SetRoundState(DRoundState.PickedGameMaster);
            
            //
            
            SetRoundState(DRoundState.StartPost);
            
        }, 0.015625f);
    }
    
    private void EndDeathrunRound()
    {
        SetRoundState(DRoundState.EndPre);

        //reset the game master
        SetGameMaster(null);
        
        SetRoundState(DRoundState.EndPost);
    }
    
    #endregion
    
    #region Game Master

    private bool PickGameMaster()
    {
        var candidateGameMasterDeathrunPlayers 
            = PlayersManager.PlayersManager.Instance
                .GetAllAliveDeathrunPlayers()
                .Where(deathrunPlayer =>
                {
                    if (deathrunPlayer is not DeathrunPlayer { } ) return false;
                    
                    //if SkipNextGameMasterPickUp is false - keep the candidate in the list
                    if (deathrunPlayer.SkipNextGameMasterPickUp is not true) return true;
                    
                    //remove the candidate from predicate and pre-add for next check
                    deathrunPlayer.SkipNextGameMasterPickUp = false;
                    return false;
                })
                .ToList();
        
        var gameMasterDeathrunPlayer = candidateGameMasterDeathrunPlayers.Count is 1 ? 
                                        candidateGameMasterDeathrunPlayers.FirstOrDefault() 
                                        : candidateGameMasterDeathrunPlayers.ElementAtOrDefault(Random.Shared.Next(candidateGameMasterDeathrunPlayers.Count));
        
        if (gameMasterDeathrunPlayer is null) return false;

        gameMasterDeathrunPlayer.ChangeClass(DPlayerClass.GameMaster);
        gameMasterDeathrunPlayer.SkipNextGameMasterPickUp = true;
        SetGameMaster(gameMasterDeathrunPlayer as DeathrunPlayer);
        return true;
    }

    #endregion
    
    #region Listener's overrides
    
    int IClientListener.ListenerVersion => IClientListener.ApiVersion;
    int IClientListener.ListenerPriority => 9;
    int IGameListener.ListenerVersion => IGameListener.ApiVersion;
    int IGameListener.ListenerPriority => 9;
    int IEntityListener.ListenerVersion => IEntityListener.ApiVersion;
    int IEntityListener.ListenerPriority => 9;
    
    #endregion
}




