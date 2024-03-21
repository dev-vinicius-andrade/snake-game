using Domain.Game.Entities;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Constants;

public class DefaultDirections
{
    public static KeyValuePair<Direction, IPointAxis> Up => new(Direction.Up, new PointAxis
    {
        X = 0,
        Y = -1,
        Z = 0
    });

    public static KeyValuePair<Direction, IPointAxis> Down => new(Direction.Down, new PointAxis
    {
        X = 0,
        Y = 1,
        Z = 0
    });

    public static KeyValuePair<Direction, IPointAxis> Left => new(Direction.Left, new PointAxis
    {
        X = -1,
        Y = 0,
        Z = 0
    });
    public static KeyValuePair<Direction, IPointAxis> Right => new(Direction.Right, new PointAxis
    {
        X = 1,
        Y = 0,
        Z = 0
    });
}