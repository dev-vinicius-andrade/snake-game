namespace Domain.Game.Rules;

/// <summary>
/// Moves a snake body like a rope: the head goes where it is told, every other segment is pulled
/// along only when it gets farther than the spacing from the segment in front of it.
/// Distances are measured the short way around the world, so wrapping edges do not stretch the body.
/// </summary>
public sealed class SnakeBodyRules
{
    public BodyState Move(BodyState body, Point newHead, int targetLength, double spacing, WorldBounds world)
    {
        ArgumentNullException.ThrowIfNull(body);
        EnsurePositive(targetLength, nameof(targetLength));
        EnsurePositive(spacing, nameof(spacing));

        var segments = FollowHead(body.Segments, newHead, spacing, world);
        var health = AlignHealth(body.Health, segments.Length);
        return Resize(segments, health, targetLength);
    }

    private static Point[] FollowHead(IReadOnlyList<Point> previous, Point newHead, double spacing, WorldBounds world)
    {
        var moved = new Point[Math.Max(previous.Count, 1)];
        moved[^1] = newHead;
        for (var index = moved.Length - 2; index >= 0; index--)
            moved[index] = Pull(previous[index], moved[index + 1], spacing, world);
        return moved;
    }

    private static Point Pull(Point follower, Point anchor, double spacing, WorldBounds world)
    {
        var dx = ShortestDelta(follower.X - anchor.X, world.Width);
        var dy = ShortestDelta(follower.Y - anchor.Y, world.Height);
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance <= spacing) return follower;

        var scale = spacing / distance;
        return new Point(Wrap(anchor.X + dx * scale, world.Width), Wrap(anchor.Y + dy * scale, world.Height));
    }

    private static double ShortestDelta(double delta, double size)
    {
        if (delta > size / 2) return delta - size;
        return delta < -size / 2 ? delta + size : delta;
    }

    private static double Wrap(double value, double size)
        => (value % size + size) % size;

    private static double[] AlignHealth(IReadOnlyList<double> health, int segmentCount)
    {
        var missing = Math.Max(0, segmentCount - health.Count);
        return Enumerable.Repeat(1.0, missing).Concat(health).TakeLast(segmentCount).ToArray();
    }

    private static BodyState Resize(Point[] segments, double[] health, int targetLength)
    {
        if (segments.Length < targetLength)
            return new BodyState(segments.Prepend(segments[0]).ToArray(), health.Prepend(1.0).ToArray());

        var excess = segments.Length - targetLength;
        return new BodyState(segments.Skip(excess).ToArray(), health.Skip(excess).ToArray());
    }

    private static void EnsurePositive(double value, string name)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(name, value, "Must be greater than zero.");
    }
}
