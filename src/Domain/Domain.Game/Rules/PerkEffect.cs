namespace Domain.Game.Rules;

/// <summary>What a perk changes. Amounts are in <see cref="PerkDefinition.Amount"/>.</summary>
public enum PerkEffect
{
    /// <summary>Amount more shots in every volley.</summary>
    ExtraShots,
    /// <summary>Amount added to the boost speed factor.</summary>
    BoostSpeed,
    /// <summary>Amount (0..1) shaved off the shot cooldown.</summary>
    FasterReload,
    /// <summary>Amount (0..1) more time boosting for each mass spent.</summary>
    EfficientBoost,
    /// <summary>Amount more shots fired, one after the other, every time you shoot.</summary>
    BurstShot,
    /// <summary>Amount added to the shot speed factor (0.5 is half as fast again).</summary>
    ShotSpeed,
    /// <summary>Holding the shoot control keeps shooting. Amount only needs to be above zero.</summary>
    AutoFire,
    /// <summary>Shots pass through snakes, hitting each body once. Amount only needs to be above zero.</summary>
    PiercingShot,
    /// <summary>Amount (0..1) less damage from shots on the body.</summary>
    Armor,
    /// <summary>Amount HP per second healed on the most damaged segment.</summary>
    Regeneration,
    /// <summary>Amount world units around the head from which food is pulled in.</summary>
    Magnet,
    /// <summary>Amount (0..1) taken off the angle between the shots of a volley, so they fly closer together.</summary>
    Precision,
    /// <summary>Amount lives: when the snake would die it is saved once for each, keeps part of its mass and is protected for a moment.</summary>
    ExtraLife,
    /// <summary>Amount (0..1) less mass lost to lightning, meteors, mines and the zone; the entry's target can limit it to one of them.</summary>
    EventResistance,
    /// <summary>Amount (0..1) of the mass of a snake you kill that you take for yourself, at once.</summary>
    Plunder,
    /// <summary>A cooldown ability: for the perk's duration nothing can hurt the snake. Amount only needs to be above zero.</summary>
    ShieldBurst,
    /// <summary>A cooldown ability: for the perk's duration the snake is Amount faster, for free.</summary>
    Dash,
    /// <summary>A cooldown ability: heals Amount HP at once, spread over the most damaged segments.</summary>
    HealPulse,
    /// <summary>A cooldown ability: for the perk's duration food within Amount world units is pulled to the head.</summary>
    VacuumBurst
}

public static class PerkEffectKinds
{
    /// <summary>The effects that are used on a key and then recharge, instead of working all the time.</summary>
    public static bool IsAbility(this PerkEffect effect) => effect is PerkEffect.ShieldBurst or PerkEffect.Dash or PerkEffect.HealPulse or PerkEffect.VacuumBurst;

    /// <summary>The effects whose amount is a share below 1.</summary>
    public static bool IsRatio(this PerkEffect effect)
        => effect is PerkEffect.FasterReload or PerkEffect.EfficientBoost or PerkEffect.Armor or PerkEffect.Precision or PerkEffect.EventResistance or PerkEffect.Plunder;

    /// <summary>The effects whose amount is a strength, so a tier multiplies it. The others are switches.</summary>
    public static bool IsSwitch(this PerkEffect effect) => effect is PerkEffect.AutoFire or PerkEffect.PiercingShot or PerkEffect.ShieldBurst;
}
