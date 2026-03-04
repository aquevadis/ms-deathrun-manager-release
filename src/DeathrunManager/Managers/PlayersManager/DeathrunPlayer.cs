using System;
using System.Threading.Tasks;
using DeathrunManager.Config;
using DeathrunManager.Extensions;
using DeathrunManager.Managers.GameplayManager;
using DeathrunManager.Shared.Enums;
using DeathrunManager.Shared.Objects;
using Microsoft.Extensions.Logging;
using Sharp.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;

namespace DeathrunManager.Managers.PlayersManager;

public class DeathrunPlayer : IDeathrunPlayer
{
    public DeathrunPlayer(IGameClient client)
    {
        Client = client;
        
        //skip initialization of the lives system if we've disabled the system
        if (LivesSystemManager.LivesSystemManager.LivesSystemConfig?.EnableLivesSystem is true)
        {
            if (InitLivesSystem() is not true)
            {
                DeathrunManager.Logger.LogCritical("[DeathrunPlayer][InitLivesSystem] Failed to initialize lives system for player {0}!", Client.SteamId);
            }
        }
    }
    
    #region DeathrunPlayer

    public IGameClient Client { get; }
    public IPlayerController? Controller => Client.GetPlayerController();
    public IPlayerPawn? PlayerPawn => Client.GetPlayerController()?.GetPlayerPawn();
    public DPlayerClass Class { get; set; } = DPlayerClass.Contestant;
    public bool InitLivesSystem()
    {
        LivesSystem = new LivesSystem(this);
        return LivesSystem is not null;
    }
    public ILivesSystem? LivesSystem { get; private set; }
    public bool IsValid => Controller?.IsValidEntity is true && PlayerPawn?.IsValidEntity is true && Controller.IsConnected() is true;
    public bool IsValidAndAlive => IsValid is true && PlayerPawn?.IsAlive is true;
    public bool SkipNextGameMasterPickUp { get; set; } = false;
    
    #endregion
    
    #region Change Class Method
    
    public void ChangeClass(DPlayerClass newClass, bool force = false)
    {
        if (force is true)
        {
            if (newClass is DPlayerClass.GameMaster)
            {
                //change class of the calling deathrun player to game master
                ChangeClass(DPlayerClass.GameMaster);
                
                //change class of the game master to contestant and respawn to go CT side
                GameplayManager.GameplayManager.Instance.GetGameMaster()?.ChangeClass(DPlayerClass.Contestant);
            }
        }
        else
        {
            //skip changing class if we are already a game master
            //or if the game master is already set
            if (this == GameplayManager.GameplayManager.Instance.GetGameMaster()
                || GameplayManager.GameplayManager.Instance.GetGameMaster() is not null)
            {
                return;
            }
        }
        
        switch (newClass)
        {
            case DPlayerClass.Contestant:
                MakeContestantInternal();
                break;
            case DPlayerClass.GameMaster:
                MakeGameMasterInternal();
                break;
            default: throw new ArgumentOutOfRangeException();
        }
    }

    #endregion
    
    #region Internal Make* Methods
    
    private void MakeGameMasterInternal()
    {
        if (IsValidAndAlive is not true) return;
        
        if (Controller?.Team is not CStrikeTeam.TE)
        {
            Controller?.SwitchTeam(CStrikeTeam.TE);
            DeathrunManager.SharedSystem.GetModSharp().PushTimer(() =>
            {
                Controller?.Respawn();
                RemoveWeapons();
            }, 0.015625f);
        }
        
        Class = DPlayerClass.GameMaster;
        
        RemoveWeapons();
    }
    
    private void MakeContestantInternal()
    {
        if (IsValidAndAlive is not true) return;

        if (Controller?.Team is not CStrikeTeam.CT)
        {
            Controller?.SwitchTeam(CStrikeTeam.CT);
            DeathrunManager.SharedSystem.GetModSharp().PushTimer(() =>
            {
                Controller?.Respawn();
                RemoveWeapons();
            }, 0.015625f);
        }
        
        Class = DPlayerClass.Contestant;
        
        RemoveWeapons();
    }
    
    #endregion
    
    #region Weapons

    private void RemoveWeapons()
    {
        for (var i = Enum.GetValues<GearSlot>().Length; i >= 0; i--)
        {
            //skip knife
            if ((GearSlot)i == GearSlot.Knife) continue;
        
            //skip pistol if contestant
            if ((GearSlot)i == GearSlot.Pistol 
                && (Class is DPlayerClass.Contestant && DeathrunManagerConfig.Config.GiveWeaponToCTs)) continue;
        
            //check if we are on the grenade slot
            if ((GearSlot)i == GearSlot.Grenades)
            {
                //iterate the grenade slot 12 times to ensure all grenades are dropped
                for (var j = 12; j >= 0; j--)
                {
                    var grenade = PlayerPawn?.GetWeaponBySlot(GearSlot.Grenades);
                    if (grenade?.IsValidEntity is not true) continue;
        
                    PlayerPawn?.DropWeapon(grenade);
                    grenade.AcceptInput("Kill");
                }
            }
        
            var weapon = PlayerPawn?.GetWeaponBySlot((GearSlot)i);
            if (weapon?.IsValidEntity is not true) continue;
        
            PlayerPawn?.DropWeapon(weapon);
            weapon.AcceptInput("Kill");
        }
        
    }
    
    #endregion
    
    #region DeathrunPlayer Thinkers
    
    public void PlayerThink()
    {
        //set data to components if any was passed
        SetCenterMenuTopRowHtml(null);
        SetCenterMenuMiddleRowHtml(null);
            
        if (LivesSystem is not null)
            SetCenterMenuBottomRowHtml(LivesSystem.GetLivesCounterHtmlString());
            
        this.PrintToCenterHtml(  (_topRowHtml is not null      ? _topRowHtml    + "<br/>" : "") 
                                 + (_middleRowHtml is not null   ? _middleRowHtml + "<br/>" : "") 
                                 + _bottomRowHtml
        );
        
        //DeathrunManager.Logger.LogInformation("[DeathrunPlayer][PlayerThink] Player {0} is thinking!", Client.SteamId);

    }
    
    public void StopPlayerThink()
    {
        
        SetCenterMenuTopRowHtml(null);
        SetCenterMenuMiddleRowHtml(null);
        SetCenterMenuBottomRowHtml(null);
    }
    
    #endregion
    
    #region Html Center Menu
    
    private string? _topRowHtml = "";
    private string? _middleRowHtml = "";
    private string? _bottomRowHtml = "";
    
    public void SetCenterMenuTopRowHtml(string? htmlString) => _topRowHtml = htmlString;
    
    public void SetCenterMenuMiddleRowHtml(string? htmlString) => _middleRowHtml = htmlString;
    
    public void SetCenterMenuBottomRowHtml(string? htmlString) => _bottomRowHtml = htmlString;
    
    #endregion
}