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
    Magnet
}
