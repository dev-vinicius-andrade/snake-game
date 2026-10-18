namespace Library.Commons.Game.Domain.Interfaces.Entities;

public interface IObstacle: ITrackableId
{
    IPointAxis InitialPosition { get; }
    IPointAxis CurrentPosition { get; set; }
    IPointAxis EndPosition { get; }
    IMovementDirection Direction { get; }
    long Size { get; }
    ITrackableId CreatedyBy { get; init; }
    /// <summary>How fast the shot flies compared with a plain one, 1 without perks.</summary>
    double SpeedFactor { get; set; }
    /// <summary>A piercing shot goes on through snakes instead of ending at the first one.</summary>
    bool Piercing { get; set; }
    /// <summary>Snakes a piercing shot has already hit, so each is hit once.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    ISet<Guid> HitPlayers { get; }
}