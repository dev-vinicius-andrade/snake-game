namespace Domain.Game.Rules;

/// <summary>Spends a heal pool on the most damaged segments first.</summary>
public sealed class BodyHealer
{
    /// <summary>
    /// Heals the segment with the lowest HP ratio (ties: closest to head, lowest index).
    /// Leftover flows to the next most damaged segment. Input is not mutated.
    /// </summary>
    public IReadOnlyList<SegmentHealth> Heal(IReadOnlyList<SegmentHealth> segments, double amount)
    {
        ArgumentNullException.ThrowIfNull(segments);
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), amount, "Must not be negative.");

        var healed = segments.ToArray();
        var remaining = amount;
        while (remaining > 0 && TryFindMostDamaged(healed, out var index))
            remaining -= HealSegment(healed, index, remaining);
        return healed;
    }

    private static bool TryFindMostDamaged(SegmentHealth[] segments, out int index)
    {
        var damaged = Enumerable.Range(0, segments.Length).Where(i => segments[i].IsDamaged).ToArray();
        index = damaged.Length == 0 ? -1 : damaged.MinBy(i => segments[i].Ratio);
        return damaged.Length > 0;
    }

    private static double HealSegment(SegmentHealth[] segments, int index, double available)
    {
        var applied = Math.Min(segments[index].MaxHp - segments[index].Hp, available);
        segments[index] = segments[index].Heal(applied);
        return applied;
    }
}
