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


}