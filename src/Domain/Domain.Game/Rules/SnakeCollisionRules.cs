namespace Domain.Game.Rules;

public readonly record struct Point(double X, double Y);

/// <summary>A snake as the rules see it. Segments are ordered tail to head, the head is the last one.</summary>
public sealed record SnakeBody(Guid Id, IReadOnlyList<Point> Segments);

/// <summary>Decides which snakes die by running their head into another snake.</summary>
public sealed class SnakeCollisionRules
{
    /// <summary>
    /// A snake dies when its head overlaps any segment, head included, of a different snake.
    /// Own body is ignored. Evaluated on a frozen picture of all snakes, so the outcome
    /// does not depend on list order and a head-on hit kills both.
    /// </summary>
    public IReadOnlySet<Guid> FindCollided(IReadOnlyList<SnakeBody> snakes, double hitSize)
    {
        ArgumentNullException.ThrowIfNull(snakes);
        if (hitSize <= 0) throw new ArgumentOutOfRangeException(nameof(hitSize), hitSize, "Must be greater than zero.");

        return snakes
            .Where(snake => snake.Segments.Count > 0)
            .Where(snake => HeadHitsAnotherSnake(snake, snakes, hitSize))
            .Select(snake => snake.Id)
            .ToHashSet();
    }

    private static bool HeadHitsAnotherSnake(SnakeBody snake, IEnumerable<SnakeBody> snakes, double hitSize)
    {
        var head = snake.Segments[^1];
        return snakes
            .Where(other => other.Id != snake.Id)
            .SelectMany(other => other.Segments)
            .Any(segment => Overlaps(head, segment, hitSize));
    }

    private static bool Overlaps(Point first, Point second, double size)
        => first.X <= second.X + size && first.X + size >= second.X
        && first.Y <= second.Y + size && first.Y + size >= second.Y;
}
