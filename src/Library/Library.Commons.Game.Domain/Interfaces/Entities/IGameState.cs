namespace Library.Commons.Game.Domain.Interfaces.Entities;

public interface IGameState
{
    IRoom Room { get; }
    IDictionary<Guid, IPlayer> Players { get; }

    IDictionary<string, IColor> Colors { get; set; }
    IDictionary<Guid, IFood> Foods { get; set; }
    IDictionary<Guid,IObstacle> Obstacles { get; set; }
    /// <summary>Spots that lightning is about to strike, by id: shown to the players until the strike.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    System.Collections.Concurrent.ConcurrentDictionary<Guid, Library.Commons.Game.Domain.Entities.WorldStrike> Strikes { get; }
    /// <summary>The boss that stands in the room, or null.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    Library.Commons.Game.Domain.Entities.WorldBoss? Boss { get; set; }
    /// <summary>Damage dealt since the last time it was read; the game loop drains it and tells the players.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    System.Collections.Concurrent.ConcurrentQueue<Library.Commons.Game.Domain.Entities.DamageDealt> Hits { get; }
}