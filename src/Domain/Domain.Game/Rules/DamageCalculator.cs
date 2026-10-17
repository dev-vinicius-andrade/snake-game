namespace Domain.Game.Rules;

/// <summary>Pure rules for segment max HP and shot damage.</summary>
public sealed class DamageCalculator
{
    private readonly DamageModelOptions _options;

    public DamageCalculator(DamageModelOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.EnsureValid();
        _options = options;
    }

    /// <summary>maxHp(L) = H0 * (1 + growth * ln(max(L, S0) / S0)).</summary>
    public double MaxSegmentHp(int length)
    {
        EnsureValidLength(length);
        var effectiveLength = Math.Max(length, _options.InitialLength);
        var growth = _options.SegmentHpGrowth * Math.Log((double)effectiveLength / _options.InitialLength);
        return _options.BaseSegmentHp * (1 + growth);
    }

    /// <summary>dmg = clamp(B * (Ls / Lt)^alpha, min, capFraction * maxHp(Lt)).</summary>
    public double ShotDamage(int shooterLength, int targetLength)
    {
        EnsureValidLength(shooterLength);
        EnsureValidLength(targetLength);
        var raw = _options.BaseShotDamage * Math.Pow((double)shooterLength / targetLength, _options.SizeRatioExponent);
        var cap = _options.MaxShotDamageFraction * MaxSegmentHp(targetLength);
        return Math.Min(Math.Max(raw, _options.MinShotDamage), cap);
    }

    /// <summary>Hits needed to break one full-HP segment.</summary>
    public int ShotsToBreak(int shooterLength, int targetLength)
        => (int)Math.Ceiling(MaxSegmentHp(targetLength) / ShotDamage(shooterLength, targetLength) - 1e-9);

    /// <summary>HP pool granted by eating fruit of the given value.</summary>
    public double HealAmount(double fruitValue)
    {
        if (fruitValue < 0) throw new ArgumentOutOfRangeException(nameof(fruitValue), fruitValue, "Must not be negative.");
        return _options.HealPerFruit * fruitValue;
    }

    private static void EnsureValidLength(int length)
    {
        if (length < 1) throw new ArgumentOutOfRangeException(nameof(length), length, "Length must be at least 1.");
    }
}
