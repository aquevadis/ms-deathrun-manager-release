using DeathrunManager.Shared.Enums;
using DeathrunManager.Shared.Objects;
using Microsoft.Extensions.Logging;
using Sharp.Shared;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;

namespace DeathrunManager.Shared;

public interface IDeathrunManager
{
    #region Identity

    /// <summary>
    /// Represents the unique identity of the <see cref="IDeathrunManager"/> interface.
    /// </summary>
    /// <remarks>
    /// This property uniquely identifies the <see cref="IDeathrunManager"/> interface and can be used
    /// for purposes such as registration, discovery, or logging.
    /// The returned value is derived from the fully qualified name of the interface or its name if the full name is null.
    /// </remarks>
    static string Identity => typeof(IDeathrunManager).FullName ?? nameof(IDeathrunManager);

    #endregion
    
    #region Players

    /// <summary>
    /// Retrieves a deathrun player associated with the specified game client.
    /// </summary>
    /// <param name="client">
    /// The game client to retrieve the associated deathrun player for.
    /// </param>
    /// <returns>
    /// An instance of <see cref="IDeathrunPlayer"/> if a valid associated player exists; otherwise, null.
    /// </returns>
    IDeathrunPlayer? GetDeathrunPlayer(IGameClient client) { return null; }

    /// <summary>
    /// Retrieves a list of all valid deathrun players.
    /// </summary>
    /// <returns>
    /// A read-only list of <see cref="IDeathrunPlayer"/> containing players that are valid.
    /// </returns>
    IReadOnlyCollection<IDeathrunPlayer> GetAllValidDeathrunPlayers() { return new List<IDeathrunPlayer>(); }

    /// <summary>
    /// Retrieves a list of all alive and valid deathrun players.
    /// </summary>
    /// <returns>
    /// A read-only list of <see cref="IDeathrunPlayer"/> containing players that are both valid and alive.
    /// </returns>
    IReadOnlyCollection<IDeathrunPlayer> GetAllAliveDeathrunPlayers() { return new List<IDeathrunPlayer>(); }

    #endregion
    
    #region Gameplay
    
    /// <summary>
    /// Retrieves the current state of the deathrun round.
    /// </summary>
    /// <returns>
    /// A value of type <see cref="DRoundState"/> indicating the current round state.
    /// </returns>
    DRoundState GetRoundState() { return DRoundState.Unset; }

    /// <summary>
    /// Retrieves the deathrun player currently designated as the game master.
    /// </summary>
    /// <returns>
    /// An instance of <see cref="IDeathrunPlayer"/> representing the game master if one is assigned; otherwise, null.
    /// </returns>
    IDeathrunPlayer? GetGameMaster() { return null; }
    
    #endregion
}
