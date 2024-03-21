using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Entities;

internal record MovementDirection(Direction Direction, IPointAxis Axis) :IMovementDirection
{
    
}