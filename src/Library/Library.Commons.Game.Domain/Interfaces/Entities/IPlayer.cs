using Library.Commons.Game.Domain.Enums;

namespace Library.Commons.Game.Domain.Interfaces.Entities;

public interface IPlayer : IId, ITrackableId
{
    string Name { get; }
    IPointAxis CurrentPosition { get; set; }
    Direction CurrentDirection { get; set; }
    IMovementDirection Direction { get; set; }
    IColor BackgroundColor { get; set; }
    IColor? BorderColor { get; set; }
    IList<IPointAxis> Path { get; set; }
    bool IsAlive { get; set; }
    long Score { get; set; }
    /// <summary>HP ratio (0..1) of each body segment, aligned with <see cref="Path"/> (tail to head).</summary>
    IList<double> SegmentHealth { get; set; }
    /// <summary>True while the player holds the boost control.</summary>
    bool IsBoosting { get; set; }
    /// <summary>Boosted ticks since the last mass was spent.</summary>
    int BoostTicks { get; set; }
    /// <summary>Simulation ticks left before the next shot is allowed.</summary>
    int ShotCooldownTicks { get; set; }
    //DateTime LastDirectionChange { get; set; }
}