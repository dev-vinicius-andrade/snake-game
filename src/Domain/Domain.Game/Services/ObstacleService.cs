using Domain.Game.Entities;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;

namespace Domain.Game.Services;

internal class ObstacleService: IObstacleService
{
    private readonly IPointAxisServices _pointAxisServices;

    public ObstacleService(IPointAxisServices pointAxisServices)
    {
        _pointAxisServices = pointAxisServices;
    }

    public IObstacle Create(ITrackableId trackableId, IRoom room, IPointAxis initialPosition,
        IMovementDirection direction,
        int size,ITrackableId createdBy)
    {
        var endPosition = CalculateEndPosition(direction, room);

        return new Obstacle(trackableId.Guid, size, room,createdBy)
        {
            InitialPosition = _pointAxisServices.CreateWith(initialPosition),
            CurrentPosition = _pointAxisServices.CreateWith(initialPosition),
            Direction = direction,
            EndPosition = endPosition
        };
    }

    public bool IsOnEndPosition(IObstacle obstacle)
    {
        var currentPosition = obstacle.CurrentPosition;
        var endPosition = obstacle.EndPosition;
        var direction = obstacle.Direction;
        var result = direction.Direction switch
        {
            Direction.Up => currentPosition.Y >= endPosition.Y, // Y increases as we move up
            Direction.Down => currentPosition.Y <= endPosition.Y, // Y decreases as we move down
            Direction.Left => currentPosition.X <= endPosition.X, // X decreases as we move left
            Direction.Right => currentPosition.X >= endPosition.X, // X increases as we move right
            _ => false // Other cases might need special handling
        };
        return result;

    }


    private IPointAxis CalculateEndPosition(IMovementDirection direction, IRoom room)
    {
        return direction.Direction switch
        {
            Direction.Up => _pointAxisServices.Create(0, room.Height), // Moving up targets the top edge of the room
            Direction.Down => _pointAxisServices.Create(0, 0), // Moving down targets the bottom edge of the room
            Direction.Left => _pointAxisServices.Create(0, 0), // Moving left targets the left edge of the room
            Direction.Right => _pointAxisServices.Create(room.Width, 0), // Moving right targets the right edge of the room
            _ => _pointAxisServices.Create(room.Width, room.Height) // Default or "Angular" might need special handling
        };
    }
}