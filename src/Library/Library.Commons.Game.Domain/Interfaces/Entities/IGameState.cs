namespace Library.Commons.Game.Domain.Interfaces.Entities;

public interface IGameState
{
    IRoom Room { get; }
    IDictionary<Guid, IPlayer> Players { get; }

    IDictionary<string, IColor> Colors { get; set; }
    IDictionary<Guid, IFood> Foods { get; set; }
    IDictionary<Guid,IObstacle> Obstacles { get; set; }
    /// <summary>Damage dealt since the last time it was read; the game loop drains it and tells the players.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    System.Collections.Concurrent.ConcurrentQueue<Library.Commons.Game.Domain.Entities.DamageDealt> Hits { get; }
}