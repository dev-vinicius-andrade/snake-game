using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Library.Commons.Game.Domain.Interfaces.Services;

public interface IObstacleService
{
    IObstacle Create(ITrackableId trackableId, IRoom room, IPointAxis initialPosition, IMovementDirection direction,
        int size, ITrackableId createdBy);

    bool IsOnEndPosition(IObstacle obstacle);
}