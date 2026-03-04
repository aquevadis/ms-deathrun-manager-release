using DeathrunManager.Shared.Enums;
using Sharp.Shared.GameEntities;
using Sharp.Shared.Objects;

namespace DeathrunManager.Shared.Objects;

public interface IDeathrunPlayer
{
    #region DeathrunPlayer
    
    /// <summary>
    /// Gets the game client associated with the player. This client provides essential
    /// functionality for interacting with the game server, managing player sessions,
    /// and facilitating communication between the client and server components.
    /// </summary>
    IGameClient Client { get; }

    /// <summary>
    /// Gets the player controller associated with this player. This controller is responsible for
    /// managing and facilitating player-specific interactions, including movement, actions, team switching,
    /// and other gameplay features during the Deathrun game mode.
    /// </summary>
    IPlayerController? Controller { get; }

    /// <summary>
    /// 
    /// </summary>
    IPlayerPawn? PlayerPawn { get; }

    /// <summary>
    /// Gets or sets the player's class in the Deathrun game, which determines their current role.
    /// The player's class can either be "Contestant" or "Game Master". The chosen class influences
    /// gameplay behavior and responsibilities within the game.
    /// </summary>
    DPlayerClass Class { get; set; }

    bool InitLivesSystem();
    
    /// <summary>
    /// Represents the lives management system associated with the player in the Deathrun context.
    /// This property provides access to functionality that allows tracking and modification of the
    /// player's remaining lives, such as adding, removing, or resetting lives; and respawning
    /// mechanics tied to life usage.
    /// </summary>
    ILivesSystem? LivesSystem { get; }

    /// <summary>
    /// Gets a value indicating whether the player's controller and player pawn are valid entities and the player is currently connected.
    /// The property returns true if all related components required to represent a functioning player are in a valid state.
    /// Otherwise, it returns false.
    /// </summary>
    bool IsValid { get; }

    /// <summary>
    /// Gets a value indicating whether the player is in a valid state and their player pawn is alive.
    /// The property returns true if all required components for a functioning player are present and properly initialized,
    /// and the player pawn is alive. Otherwise, it returns false.
    /// </summary>
    bool IsValidAndAlive { get; }
    
    /// <summary>
    /// Gets or sets a value indicating whether the player should be excluded from being selected as the Game Master in the next Game Master selection process.
    /// If set to true, the player will be skipped for the upcoming selection and the value will reset to false afterward.
    /// </summary>
    bool SkipNextGameMasterPickUp { get; set; }
    
    #endregion
    
    #region Change Class Method

    /// <summary>
    /// Changes the class of the player to the specified new class.
    /// This method allows switching between Contestant and GameMaster roles
    /// and handles necessary internal changes depending on the selected class.
    /// If forced, the GameMaster role swaps with the Contestant role under specific conditions.
    /// </summary>
    /// <param name="newClass">The new player class to switch to. Possible values are Contestant or GameMaster.</param>
    /// <param name="force">Indicates whether the change should forcibly swap roles between game master and contestant.</param>
    void ChangeClass(DPlayerClass newClass, bool force = false);

    #endregion
    
    #region Html Center Menu

    /// <summary>
    /// Sets the HTML content for the top row of the center menu.
    /// This content is displayed to the player as part of the UI.
    /// </summary>
    /// <param name="htmlString">The HTML string to set for the top row. Can be null to clear the content.</param>
    void SetCenterMenuTopRowHtml(string? htmlString);

    /// <summary>
    /// Sets the HTML content for the middle row of the center menu.
    /// This content is displayed to the player as part of the UI.
    /// </summary>
    /// <param name="htmlString">The HTML string to set for the middle row. Can be null to clear the content.</param>
    void SetCenterMenuMiddleRowHtml(string? htmlString);

    /// <summary>
    /// Sets the HTML content for the bottom row of the center menu.
    /// This content is displayed to the player as part of the UI.
    /// </summary>
    /// <param name="htmlString">The HTML string to set for the bottom row. Can be null to clear the content.</param>
    void SetCenterMenuBottomRowHtml(string? htmlString);

    #endregion

    #region DeathrunPlayer Thinkers

    /// <summary>
    /// Performs periodic logic processing for the player entity.
    /// This method updates the player's center menu display by setting the top, middle,
    /// and bottom rows with relevant data and formatting them into HTML content.
    /// If a lives system is attached to the player, its HTML string representation of the
    /// lives counter is displayed on the bottom row. Finally, the constructed HTML content
    /// is rendered to the player's center screen.
    /// </summary>
    void PlayerThink();

    #endregion
}