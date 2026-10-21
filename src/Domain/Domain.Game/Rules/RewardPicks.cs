using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Rules;

/// <summary>What a prize, the turret, the bounty or the maze prize gives, from the catalog rewards it picked.</summary>
public static class RewardPicks
{
    /// <summary>
    /// All the picked rewards as one, leaving out those <paramref name="room"/> switched off. With nothing picked it is the
    /// <paramref name="embedded"/> reward the thing was written with (a config from before the catalog).
    /// </summary>
    public static PerkReward For(PerkReward embedded, IReadOnlyList<RewardDefinition> picked, IRoom? room)
        => picked.Count == 0
            ? embedded
            : PerkReward.Combine(picked.Where(reward => RoomNames.Allows("reward", reward.Id, [], [], room, offUntilNamed: false)).Select(reward => reward.Reward));
}
