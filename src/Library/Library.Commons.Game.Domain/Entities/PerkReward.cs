namespace Library.Commons.Game.Domain.Entities;

/// <summary>
/// What a prize, the turret, a bounty or the maze prize gives besides mass and foods credit. Every field off (the default) gives nothing,
/// so a config written before rewards existed keeps working. The perk that is given by name (<c>GrantsPerk</c>) stays where it was.
/// </summary>
public sealed record PerkReward
{
    /// <summary>Gives a perk the snake does not have yet, picked at random among those that work in the room. Nothing when it has them all.</summary>
    public bool GrantsRandomPerk { get; init; }

    /// <summary>Lives given on top of the ones perks give: the snake is saved this many more times from dying.</summary>
    public int ExtraLives { get; init; }

    /// <summary>Tiers a perk the snake has is lifted by, beyond what its foods give. 0: no surge. The amount is picked between this and <see cref="TierSurgeMax"/>.</summary>
    public int TierSurgeMin { get; init; }

    /// <summary>The most tiers a surge lifts a perk by. Below <see cref="TierSurgeMin"/> counts as equal to it.</summary>
    public int TierSurgeMax { get; init; }

    /// <summary>The perk to lift. Empty: a random one of those the snake has that can still go up.</summary>
    public string? TierSurgePerk { get; init; }

    /// <summary>Mass given at once, apart from the mass of the prize itself.</summary>
    public long BonusMass { get; init; }

    /// <summary>Heals the whole body at once.</summary>
    public bool Mend { get; init; }

    public bool HasTierSurge => TierSurgeMin > 0 || TierSurgeMax > 0;

    public bool IsEmpty => !GrantsRandomPerk && ExtraLives <= 0 && !HasTierSurge && BonusMass <= 0 && !Mend;

    /// <summary>Most lives, tiers or mass one reward may give, so a typo cannot make a snake immortal.</summary>
    public const int MaxLives = 5;
    public const int MaxSurge = 10;
    public const long MaxBonusMass = 10_000;

    /// <summary>The reason the reward cannot be used, or null.</summary>
    public string? Problem()
    {
        if (ExtraLives is < 0 or > MaxLives) return $"extra lives must be 0 to {MaxLives}";
        if (TierSurgeMin is < 0 or > MaxSurge || TierSurgeMax is < 0 or > MaxSurge) return $"a tier surge must be 0 to {MaxSurge} tiers";
        if (BonusMass is < 0 or > MaxBonusMass) return $"bonus mass must be 0 to {MaxBonusMass}";
        return null;
    }
}

/// <summary>When a cooldown perk can be used again, and until when its effect lasts. Both are server clock times.</summary>
public readonly record struct AbilityTimer(DateTimeOffset ReadyAt, DateTimeOffset ActiveUntil);
