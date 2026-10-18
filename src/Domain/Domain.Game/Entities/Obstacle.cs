using System.Text.Json.Serialization;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Entities;

[Serializable]
public record Obstacle(Guid Guid, long Size, IRoom Room, ITrackableId CreatedyBy) :IObstacle
{
    
    public Guid Guid { get; } = Guid;

    [JsonPropertyName("initialPosition")]
    public IPointAxis InitialPosition { get; init; } = null!;
    [JsonPropertyName("currentPosition")]
    public IPointAxis CurrentPosition { get; set; } = null!;
    public IPointAxis EndPosition { get; init; } = null!;
    public IMovementDirection Direction { get; init; } = null!;
    public long Size { get; } = Size;
    public double SpeedFactor { get; set; } = 1.0;
    public bool Piercing { get; set; }
    [JsonIgnore]
    public ISet<Guid> HitPlayers { get; } = new HashSet<Guid>();


}