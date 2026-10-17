namespace Domain.Game.Rules;

/// <summary>Heals a body stored as HP ratios (0..1) for a given max HP per segment.</summary>
public sealed class BodyHealthRules
{
    private readonly BodyHealer _healer;

    public BodyHealthRules(BodyHealer healer)
    {
        ArgumentNullException.ThrowIfNull(healer);
        _healer = healer;
    }

    public IReadOnlyList<double> Heal(IReadOnlyList<double> ratios, double maxHp, double amount)
    {
        ArgumentNullException.ThrowIfNull(ratios);
        if (maxHp <= 0) throw new ArgumentOutOfRangeException(nameof(maxHp), maxHp, "Must be greater than zero.");
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), amount, "Must not be negative.");

        var segments = ratios.Select(ratio => SegmentHealth.Of(ratio * maxHp, maxHp)).ToArray();
        return _healer.Heal(segments, amount).Select(segment => segment.Ratio).ToArray();
    }
}
