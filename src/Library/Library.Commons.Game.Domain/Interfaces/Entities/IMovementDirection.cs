using Library.Commons.Game.Domain.Enums;

namespace Library.Commons.Game.Domain.Interfaces.Entities;

public interface IMovementDirection
{
    Direction Direction { get; }
    IPointAxis Axis { get; }
}