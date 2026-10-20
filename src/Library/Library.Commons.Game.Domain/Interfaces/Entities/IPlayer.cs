using System.Text.Json.Serialization;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Enums;

namespace Library.Commons.Game.Domain.Interfaces.Entities;

public interface IPlayer : IId, ITrackableId
{
    string Name { get; }
    /// <summary>The account of a signed-in player; null for a guest. Its stats are counted only when set. Never sent to clients.</summary>
    [JsonIgnore]
    Guid? AccountId { get; set; }
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
    /// <summary>The tier each of <see cref="Perks"/> is at, 1 for the first, in the same order.</summary>
    IList<int> PerkTiers { get; set; }
    /// <summary>Ids of perks the snake was given, for example by a prize, whatever the foods it has eaten. Lost with the snake.</summary>
    [JsonIgnore]
    IList<string> GrantedPerks { get; set; }
    /// <summary>Tiers a reward lifted each perk by, beyond its foods. Lost with the snake.</summary>
    [JsonIgnore]
    IDictionary<string, int> TierSurges { get; set; }
    /// <summary>Lives rewards gave, on top of the ones perks give.</summary>
    [JsonIgnore]
    int BonusLives { get; set; }
    /// <summary>Lives already used up in this life, to be saved from dying.</summary>
    [JsonIgnore]
    int LivesUsed { get; set; }
    /// <summary>Nothing can hurt the snake until this moment (after a life saved it). Null: not protected.</summary>
    [JsonIgnore]
    DateTimeOffset? ProtectedUntil { get; set; }
    /// <summary>For each cooldown perk (by id): when it can be used again and until when it is on.</summary>
    [JsonIgnore]
    IDictionary<string, AbilityTimer> Abilities { get; set; }
    /// <summary>True when the player asked for cooldown perks to be used by themselves (for the eye controls, which have no spare gesture).</summary>
    [JsonIgnore]
    bool AutoAbilities { get; set; }
    /// <summary>True when a perk lets the snake keep shooting while the shoot control is held.</summary>
    bool AutoFire { get; set; }
    /// <summary>Shots of a burst still to fire.</summary>
    [JsonIgnore]
    int BurstRemaining { get; set; }
    /// <summary>Ticks until the next shot of the burst.</summary>
    [JsonIgnore]
    int BurstTicks { get; set; }
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