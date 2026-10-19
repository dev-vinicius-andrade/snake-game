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
        => FindCollisions(snakes, hitSize).Keys.ToHashSet();

    /// <summary>The snakes that collided, each with the snake it ran into: the one whose body (or head) its head overlapped first.</summary>
    public IReadOnlyDictionary<Guid, Guid> FindCollisions(IReadOnlyList<SnakeBody> snakes, double hitSize)
    {
        ArgumentNullException.ThrowIfNull(snakes);
        if (hitSize <= 0) throw new ArgumentOutOfRangeException(nameof(hitSize), hitSize, "Must be greater than zero.");

        var collisions = new Dictionary<Guid, Guid>();
        foreach (var snake in snakes.Where(snake => snake.Segments.Count > 0))
            if (SnakeRunInto(snake, snakes, hitSize) is { } other)
                collisions[snake.Id] = other;
        return collisions;
    }

    private static Guid? SnakeRunInto(SnakeBody snake, IEnumerable<SnakeBody> snakes, double hitSize)
    {
        var head = snake.Segments[^1];
        return snakes
            .Where(other => other.Id != snake.Id)
            .Where(other => other.Segments.Any(segment => HitBox.Overlaps(head, segment, hitSize)))
            .Select(other => (Guid?)other.Id)
            .FirstOrDefault();
    }
}
