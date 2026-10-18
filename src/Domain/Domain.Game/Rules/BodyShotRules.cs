namespace Domain.Game.Rules;

public readonly record struct ShotHit(int SegmentIndex, bool IsHead);

public sealed record ShotDamageOutcome(BodyState Body, Point? BrokenAt)
{
    public bool SegmentBroken => BrokenAt.HasValue;
}

/// <summary>Where a shot lands on a snake and what the damage does to a body segment.</summary>
public sealed class BodyShotRules
{
    /// <summary>The head wins over the body. Among body segments the one nearest to the head is hit.</summary>
    public ShotHit? FindHit(IReadOnlyList<Point> segments, Point shot, double hitSize)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (segments.Count == 0) return null;

        var headIndex = segments.Count - 1;
        if (HitBox.Overlaps(segments[headIndex], shot, hitSize)) return new ShotHit(headIndex, true);

        var bodyIndex = Enumerable.Range(0, headIndex).Reverse()
            .Cast<int?>()
            .FirstOrDefault(index => HitBox.Overlaps(segments[index!.Value], shot, hitSize));
        return bodyIndex.HasValue ? new ShotHit(bodyIndex.Value, false) : null;
    }

    /// <summary>
    /// Subtracts damage (in HP) from a body segment. A segment that reaches 0 HP is removed when
    /// <paramref name="canBreak"/> is true, otherwise it stays at 0 HP. The head is never damaged this way:
    /// a shot on the head kills.
    /// </summary>
    public ShotDamageOutcome ApplyDamage(BodyState body, int segmentIndex, double damage, double maxHp, bool canBreak)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (segmentIndex < 0 || segmentIndex >= body.Segments.Count - 1)
            throw new ArgumentOutOfRangeException(nameof(segmentIndex), segmentIndex, "Must be a body segment, not the head.");
        if (damage < 0) throw new ArgumentOutOfRangeException(nameof(damage), damage, "Must not be negative.");

        var hurt = SegmentHealth.Of(body.Health[segmentIndex] * maxHp, maxHp).TakeDamage(damage);
        return hurt.IsBroken && canBreak
            ? Break(body, segmentIndex)
            : new ShotDamageOutcome(body with { Health = Replace(body.Health, segmentIndex, hurt.Ratio) }, null);
    }

    private static ShotDamageOutcome Break(BodyState body, int segmentIndex)
        => new(
            body with { Segments = RemoveAt(body.Segments, segmentIndex), Health = RemoveAt(body.Health, segmentIndex) },
            body.Segments[segmentIndex]);

    private static T[] RemoveAt<T>(IReadOnlyList<T> items, int index)
        => items.Where((_, position) => position != index).ToArray();

    private static double[] Replace(IReadOnlyList<double> items, int index, double value)
        => items.Select((item, position) => position == index ? value : item).ToArray();
}
