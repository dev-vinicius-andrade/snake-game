namespace Domain.Game.Rules;

/// <summary>
/// Moves a snake body along the path its head took: every segment sits exactly one spacing behind the one in front of it,
/// measured along the trail of positions the head has been in. A body that follows the segment in front instead
/// (a rope) spirals in on itself when the snake runs in a circle, and the tail stops moving.
/// Distances are measured the short way around the world, so wrapping edges do not stretch the body.
/// </summary>
public sealed class SnakeBodyRules
{
    public BodyState Move(BodyState body, Point newHead, int targetLength, double spacing, WorldBounds world)
    {
        ArgumentNullException.ThrowIfNull(body);
        EnsurePositive(targetLength, nameof(targetLength));
        EnsurePositive(spacing, nameof(spacing));

        var trail = ExtendTrail(body, newHead, world);
        var count = Math.Max(body.Segments.Count, 1);
        var segments = PlaceAlongTrail(trail, count, spacing, world);
        var health = AlignHealth(body.Health, segments.Length);
        var resized = Resize(segments, health, targetLength);
        return resized with { Trail = Trim(trail, Math.Max(count, targetLength) + 1, spacing, world) };
    }

    /// <summary>The trail plus the new head. A snake that has no trail yet starts from its segments.</summary>
    private static List<Point> ExtendTrail(BodyState body, Point newHead, WorldBounds world)
    {
        var trail = new List<Point>(body.Trail.Count > 0 ? body.Trail : body.Segments);
        if (trail.Count == 0 || Distance(trail[^1], newHead, world) > 0) trail.Add(newHead);
        return trail;
    }

    /// <summary>Segment positions, tail to head: the head at the end of the trail, the rest at multiples of the spacing along it.</summary>
    private static Point[] PlaceAlongTrail(IReadOnlyList<Point> trail, int count, double spacing, WorldBounds world)
    {
        var placed = new Point[count];
        placed[count - 1] = trail[^1];
        var index = trail.Count - 1;
        var travelled = 0.0;
        for (var behind = 1; behind < count; behind++)
        {
            var wanted = behind * spacing;
            var length = 0.0;
            while (index > 0 && travelled + (length = Distance(trail[index], trail[index - 1], world)) < wanted)
            {
                travelled += length;
                index--;
            }
            // Past the oldest point: the extra segments stay stacked on the tail, as new segments do until the snake has grown into them.
            placed[count - 1 - behind] = index == 0 ? trail[0] : Interpolate(trail[index], trail[index - 1], (wanted - travelled) / length, world);
        }
        return placed;
    }

    /// <summary>Keeps only the part of the trail the body can still reach, plus one older point to interpolate toward.</summary>
    private static IReadOnlyList<Point> Trim(List<Point> trail, int segments, double spacing, WorldBounds world)
    {
        var needed = segments * spacing;
        var travelled = 0.0;
        for (var index = trail.Count - 1; index > 0; index--)
        {
            travelled += Distance(trail[index], trail[index - 1], world);
            if (travelled >= needed) return trail.GetRange(index - 1, trail.Count - index + 1);
        }
        return trail;
    }

    private static Point Interpolate(Point from, Point to, double fraction, WorldBounds world)
    {
        var dx = ShortestDelta(to.X - from.X, world.Width);
        var dy = ShortestDelta(to.Y - from.Y, world.Height);
        return new Point(Wrap(from.X + dx * fraction, world.Width), Wrap(from.Y + dy * fraction, world.Height));
    }

    private static double Distance(Point a, Point b, WorldBounds world)
    {
        var dx = ShortestDelta(a.X - b.X, world.Width);
        var dy = ShortestDelta(a.Y - b.Y, world.Height);
        return Math.Sqrt(dx * dx + dy * dy);
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
