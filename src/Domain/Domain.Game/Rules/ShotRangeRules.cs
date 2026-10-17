namespace Domain.Game.Rules;

/// <summary>How far a shot flies: until it leaves the room, whatever direction it was fired in.</summary>
public sealed class ShotRangeRules
{
    /// <summary>Where a ray from <paramref name="origin"/> along <paramref name="axis"/> first leaves the room.</summary>
    public Point EndPoint(Point origin, Point axis, double width, double height)
    {
        if (width <= 0) throw new ArgumentOutOfRangeException(nameof(width), width, "Must be greater than zero.");
        if (height <= 0) throw new ArgumentOutOfRangeException(nameof(height), height, "Must be greater than zero.");

        var distance = Math.Min(DistanceToEdge(origin.X, axis.X, width), DistanceToEdge(origin.Y, axis.Y, height));
        if (double.IsInfinity(distance)) return origin;

        var travel = Math.Max(0, distance);
        return new Point(origin.X + axis.X * travel, origin.Y + axis.Y * travel);
    }

    /// <summary>True once the shot has travelled as far from its start as the end point is.</summary>
    public bool HasReachedEnd(Point origin, Point current, Point end)
        => Distance(origin, current) >= Distance(origin, end) - 1e-9;

    private static double DistanceToEdge(double position, double axis, double size)
    {
        if (axis > 0) return (size - position) / axis;
        return axis < 0 ? -position / axis : double.PositiveInfinity;
    }

    private static double Distance(Point from, Point to)
        => Math.Sqrt(Math.Pow(to.X - from.X, 2) + Math.Pow(to.Y - from.Y, 2));
}
