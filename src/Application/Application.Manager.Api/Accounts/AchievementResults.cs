namespace Application.Manager.Api.Accounts;

/// <summary>The error codes of the stats api (docs/ACHIEVEMENTS.md), as they appear in <c>{ "error": "&lt;code&gt;" }</c>.</summary>
public static class StatsErrors
{
    /// <summary>The report has no id, or a known counter or maximum out of range: it was refused as a whole.</summary>
    public const string InvalidStats = "invalidStats";

    public const string UnknownAccount = "unknownAccount";
}

/// <summary>The ids of the achievements a report unlocked, in the order of the definitions. Empty when it unlocked none or was sent before.</summary>
public sealed record UnlockedAchievements(IReadOnlyList<string> Ids);

public sealed record UnlockedAchievement(string Id, DateTimeOffset UnlockedAt);

/// <summary>
/// What a player sees on its profile: the account, every <c>StatKeys</c> key with its value (0 when never reported), and the achievements it
/// unlocked, earliest first.
/// </summary>
public sealed record AccountProfile(Account Account, IReadOnlyDictionary<string, long> Stats, IReadOnlyList<UnlockedAchievement> Achievements);
