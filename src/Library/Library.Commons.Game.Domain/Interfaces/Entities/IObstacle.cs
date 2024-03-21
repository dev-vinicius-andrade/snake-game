using System.Numerics;

namespace Library.Commons.Game.Domain.Interfaces.Entities;

public interface IObstacle: ITrackableId
{
    IPointAxis InitialPosition { get; }
    IPointAxis CurrentPosition { get; set; }
    IPointAxis EndPosition { get; }
    IMovementDirection Direction { get; }
    long Size { get; }
    ITrackableId CreatedyBy { get; init; }
}