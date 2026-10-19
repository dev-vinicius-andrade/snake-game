namespace Library.Commons.Game.Domain.Entities;

/// <summary>Where a safe zone is in its life.</summary>
public enum WorldZonePhase
{
    /// <summary>Shown and covering everything: nobody is harmed yet.</summary>
    Warning = 0,
    /// <summary>Closing in: snakes outside pay.</summary>
    Closing = 1,
    /// <summary>At its smallest.</summary>
    Holding = 2
}

/// <summary>The safe zone of a room: a circle snakes had better be in. Replaced by the game loop as it closes.</summary>
/// <param name="Radius">How far from the middle it reaches now.</param>
/// <param name="FinalRadius">How far it will reach when it has closed.</param>
/// <param name="PhaseEndsAt">When it gives way to the next phase (to going, for the last).</param>
public sealed record WorldZone(double X, double Y, double Radius, double FinalRadius, WorldZonePhase Phase, DateTimeOffset PhaseEndsAt);
