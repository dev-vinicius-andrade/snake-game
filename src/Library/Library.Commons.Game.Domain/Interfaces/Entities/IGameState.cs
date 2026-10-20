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
    /// <summary>The safe zone of the room, or null.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    Library.Commons.Game.Domain.Entities.WorldZone? Zone { get; set; }
    /// <summary>Mines lying on the map, by id, until a snake sets one off.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    System.Collections.Concurrent.ConcurrentDictionary<Guid, Library.Commons.Game.Domain.Entities.WorldMine> Mines { get; }
    /// <summary>The snake that is wanted, or null.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    Library.Commons.Game.Domain.Entities.WorldBounty? Bounty { get; set; }
    /// <summary>When the fruit shower that is going on ends, or null while there is none.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    DateTimeOffset? FruitShowerEndsAt { get; set; }
    /// <summary>When the sugar rush that is going on ends (boosting costs no mass until then), or null while there is none.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    DateTimeOffset? SugarRushEndsAt { get; set; }
    /// <summary>The boss that stands in the room, or null.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    Library.Commons.Game.Domain.Entities.WorldBoss? Boss { get; set; }
    /// <summary>Damage dealt since the last time it was read; the game loop drains it and tells the players.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    System.Collections.Concurrent.ConcurrentQueue<Library.Commons.Game.Domain.Entities.DamageDealt> Hits { get; }
}