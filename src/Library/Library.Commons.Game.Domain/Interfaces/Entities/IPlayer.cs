using System.Text.Json.Serialization;
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
    /// <summary>
    /// Where the head has been, oldest first and the head last. The body is laid out along it, so it never goes to clients.
    /// </summary>
    [JsonIgnore]
    IList<IPointAxis> Trail { get; set; }
    bool IsAlive { get; set; }
    long Score { get; set; }
    /// <summary>HP ratio (0..1) of each body segment, aligned with <see cref="Path"/> (tail to head).</summary>
    IList<double> SegmentHealth { get; set; }
    /// <summary>True while the player holds the boost control.</summary>
    /// <summary>Foods eaten in this life. Perks are earned from it.</summary>
    int FoodsEaten { get; set; }
    /// <summary>Ids of the perks the snake has, in the order they were earned.</summary>
    IList<string> Perks { get; set; }
    bool IsBoosting { get; set; }
    /// <summary>Boosted ticks since the last mass was spent.</summary>
    int BoostTicks { get; set; }
    /// <summary>Simulation ticks left before the next shot is allowed.</summary>
    int ShotCooldownTicks { get; set; }
    /// <summary>Direction of travel in radians: 0 is right, positive turns clockwise on screen (up is -PI/2).</summary>
    double Heading { get; set; }
    /// <summary>Heading the snake is turning toward. Equal to <see cref="Heading"/> when not steering.</summary>
    double TargetHeading { get; set; }
    //DateTime LastDirectionChange { get; set; }
}