using Domain.Game.Entities;
using Domain.Game.Rules;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;

namespace Domain.Game.Services;

internal class ObstacleService: IObstacleService
{
    private readonly IPointAxisServices _pointAxisServices;
    private readonly ShotRangeRules _shotRangeRules;

    public ObstacleService(IPointAxisServices pointAxisServices, ShotRangeRules shotRangeRules)
    {
        _pointAxisServices = pointAxisServices;
        _shotRangeRules = shotRangeRules;
    }

    public IObstacle Create(ITrackableId trackableId, IRoom room, IPointAxis initialPosition,
        IMovementDirection direction,
        int size,ITrackableId createdBy)
    {
        var end = _shotRangeRules.EndPoint(ToPoint(initialPosition), ToPoint(direction.Axis), room.Width, room.Height);
        var endPosition = _pointAxisServices.CreateWith(initialPosition);
        endPosition.X = end.X;
        endPosition.Y = end.Y;

        return new Obstacle(trackableId.Guid, size, room,createdBy)
        {
            InitialPosition = _pointAxisServices.CreateWith(initialPosition),
            CurrentPosition = _pointAxisServices.CreateWith(initialPosition),
            Direction = direction,
            EndPosition = endPosition
        };
    }

    public bool IsOnEndPosition(IObstacle obstacle)
        => _shotRangeRules.HasReachedEnd(ToPoint(obstacle.InitialPosition), ToPoint(obstacle.CurrentPosition), ToPoint(obstacle.EndPosition));

    private static Point ToPoint(IPointAxis axis) => new(axis.X, axis.Y);
}
