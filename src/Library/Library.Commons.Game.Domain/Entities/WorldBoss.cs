namespace Library.Commons.Game.Domain.Entities;

/// <summary>
/// A boss that stands in a room: snakes shoot it down for a reward, and it fires back. Only one at a time. Changed only by the
/// game loop of its room.
/// </summary>
public sealed class WorldBoss
{
    public Guid Id { get; init; }

    /// <summary>The middle of it, in world units.</summary>
    public double X { get; init; }
    public double Y { get; init; }

    /// <summary>Side of its square, in world units.</summary>
    public double Size { get; init; }

    /// <summary>Hit points left: every shot takes one.</summary>
    public double Hp { get; set; }
    public int MaxHp { get; init; }

    /// <summary>Which way the first arm points, in radians; it turns as the boss fires. For the picture only.</summary>
    public double Angle { get; set; }

    /// <summary>Shots in a volley, for the picture.</summary>
    public int Arms { get; init; }

    /// <summary>How fast its shots fly compared with a snake shot.</summary>
    public double ShotSpeed { get; init; } = 1;

    /// <summary>Mass that falls as food where it is brought down.</summary>
    public long RewardMass { get; init; }

    /// <summary>How many foods bringing it down counts for toward the perks of the snake that does.</summary>
    public int FoodsCredit { get; init; }

    /// <summary>The perk the snake that brings it down gets, if any.</summary>
    public string? GrantsPerk { get; init; }

    /// <summary>The snake that shot it last: the one that brings it down.</summary>
    public Guid? LastHitBy { get; set; }
}
