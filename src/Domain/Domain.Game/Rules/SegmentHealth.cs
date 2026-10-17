namespace Domain.Game.Rules;

/// <summary>Immutable HP of one body segment. Every change returns a new instance.</summary>
public readonly record struct SegmentHealth
{
    public double Hp { get; }
    public double MaxHp { get; }

    private SegmentHealth(double hp, double maxHp)
    {
        Hp = hp;
        MaxHp = maxHp;
    }

    public static SegmentHealth Full(double maxHp)
    {
        EnsurePositiveMax(maxHp);
        return new SegmentHealth(maxHp, maxHp);
    }

    public static SegmentHealth Of(double hp, double maxHp)
    {
        EnsurePositiveMax(maxHp);
        return new SegmentHealth(Math.Clamp(hp, 0, maxHp), maxHp);
    }

    public double Ratio => Hp / MaxHp;
    public bool IsBroken => Hp <= 0;
    public bool IsDamaged => Hp < MaxHp;

    public SegmentHealth TakeDamage(double damage)
    {
        EnsureNotNegative(damage, nameof(damage));
        return new SegmentHealth(Math.Max(0, Hp - damage), MaxHp);
    }

    public SegmentHealth Heal(double amount)
    {
        EnsureNotNegative(amount, nameof(amount));
        return new SegmentHealth(Math.Min(MaxHp, Hp + amount), MaxHp);
    }

    /// <summary>Changes max HP keeping the HP ratio, so growing never heals nor hurts.</summary>
    public SegmentHealth WithMaxHp(double newMaxHp)
        => Of(Ratio * newMaxHp, newMaxHp);

    private static void EnsurePositiveMax(double maxHp)
    {
        if (maxHp <= 0) throw new ArgumentOutOfRangeException(nameof(maxHp), maxHp, "Must be greater than zero.");
    }

    private static void EnsureNotNegative(double value, string name)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(name, value, "Must not be negative.");
    }
}
