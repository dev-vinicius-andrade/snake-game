namespace Domain.Game.Rules;

/// <summary>Tunable constants of the segment HP and shot damage model. See docs/GAME_DESIGN.md.</summary>
public sealed record DamageModelOptions
{
    public const string SectionName = "DamageModel";

    public int InitialLength { get; init; } = 5;
    public double BaseSegmentHp { get; init; } = 100;
    public double SegmentHpGrowth { get; init; } = 1.0;
    public double BaseShotDamage { get; init; } = 40;
    public double SizeRatioExponent { get; init; } = 0.5;
    public double MinShotDamage { get; init; } = 5;
    public double MaxShotDamageFraction { get; init; } = 0.5;
    public double HealPerFruit { get; init; } = 30;

    public void EnsureValid()
    {
        EnsurePositive(InitialLength, nameof(InitialLength));
        EnsurePositive(BaseSegmentHp, nameof(BaseSegmentHp));
        EnsurePositive(BaseShotDamage, nameof(BaseShotDamage));
        EnsurePositive(SizeRatioExponent, nameof(SizeRatioExponent));
        EnsurePositive(MinShotDamage, nameof(MinShotDamage));
        EnsurePositive(HealPerFruit, nameof(HealPerFruit));
        EnsureFraction(MaxShotDamageFraction, nameof(MaxShotDamageFraction));
        EnsureNotNegative(SegmentHpGrowth, nameof(SegmentHpGrowth));
    }

    private static void EnsurePositive(double value, string name)
    {
        if (value <= 0) throw new ArgumentOutOfRangeException(name, value, "Must be greater than zero.");
    }

    private static void EnsureNotNegative(double value, string name)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(name, value, "Must not be negative.");
    }

    private static void EnsureFraction(double value, string name)
    {
        if (value is <= 0 or > 1) throw new ArgumentOutOfRangeException(name, value, "Must be in (0, 1].");
    }
}
