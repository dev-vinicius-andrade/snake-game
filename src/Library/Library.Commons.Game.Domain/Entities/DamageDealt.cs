namespace Library.Commons.Game.Domain.Entities;

/// <summary>What a shot did to a snake, kept for a moment so the players involved can be told.</summary>
/// <param name="Damage">HP taken from the segment that was hit; 0 for a shot on the head.</param>
/// <param name="Kind">How it ended for the target.</param>
public readonly record struct DamageDealt(
    Guid ShooterGuid, Guid TargetGuid, double X, double Y, double Damage, DamageKind Kind);

public enum DamageKind
{
    /// <summary>A segment lost some HP.</summary>
    Hit,
    /// <summary>A segment lost all its HP and broke off.</summary>
    Broke,
    /// <summary>The shot hit the head and the snake died.</summary>
    Kill,
    /// <summary>
    /// Not a shot: the snake lost mass to lightning, a meteor or the zone. <c>Damage</c> is the mass lost and the position is the head.
    /// Sent only to the snake that lost it.
    /// </summary>
    Lost
}
