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
    EfficientBoost
}
