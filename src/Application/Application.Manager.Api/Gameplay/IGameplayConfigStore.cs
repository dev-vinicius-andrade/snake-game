using Domain.Game.Rules;

namespace Application.Manager.Api.Gameplay;

/// <summary>Where the gameplay config the game servers run on is kept: a file (<see cref="GameplayConfigStore"/>) or the database tables.</summary>
public interface IGameplayConfigStore
{
    /// <summary>The config in force and its revision.</summary>
    GameplayDocument Current { get; }

    /// <summary>Saves a config that holds up and makes it the current one. Throws, and changes nothing, when it does not.</summary>
    GameplayDocument Update(GameplayConfig config);
}
