namespace Library.Commons.Game.Domain.Entities;

/// <summary>The numbers kept for a signed-in player over all the lives it plays. See docs/ACHIEVEMENTS.md.</summary>
public static class StatKeys
{
    // Counters: a game server reports how much one life added, the manager adds it up.
    public const string Games = "games";
    public const string Deaths = "deaths";
    public const string Kills = "kills";
    public const string Foods = "foods";
    public const string Prizes = "prizes";
    public const string BossKills = "bossKills";
    public const string LightningHits = "lightningHits";
    public const string MeteorHits = "meteorHits";
    public const string PlaySeconds = "playSeconds";

    // Maxima: a game server reports the best of one life, the manager keeps the highest ever.
    public const string BestScore = "bestScore";
    public const string LongestSnake = "longestSnake";
    public const string BestKillsInLife = "bestKillsInLife";

    public static IReadOnlyList<string> Counters { get; } =
        [Games, Deaths, Kills, Foods, Prizes, BossKills, LightningHits, MeteorHits, PlaySeconds];

    public static IReadOnlyList<string> Maxima { get; } = [BestScore, LongestSnake, BestKillsInLife];

    public static bool IsCounter(string key) => Counters.Contains(key);

    public static bool IsMaximum(string key) => Maxima.Contains(key);
}

/// <summary>An achievement: unlocked once a stat reaches a threshold. Texts are in the client, by id (en and pt).</summary>
/// <param name="Icon">A game-icons.net name such as game-icons:snake (see docs/PERKS.md, Icons).</param>
public sealed record AchievementDefinition(string Id, string Icon, string Stat, long Threshold);

public static class Achievements
{
    public static IReadOnlyList<AchievementDefinition> Defaults { get; } =
    [
        new("first-steps", "game-icons:snake", StatKeys.Games, 1),
        new("regular", "game-icons:hourglass", StatKeys.Games, 25),
        new("marathon", "game-icons:sands-of-time", StatKeys.PlaySeconds, 3600),
        new("first-blood", "game-icons:pointy-sword", StatKeys.Kills, 1),
        new("hunter", "game-icons:crosshair", StatKeys.Kills, 25),
        new("slayer", "game-icons:burning-skull", StatKeys.Kills, 100),
        new("rampage", "game-icons:fire-ray", StatKeys.BestKillsInLife, 5),
        new("glutton", "game-icons:sparkles", StatKeys.Foods, 500),
        new("big-snake", "game-icons:dragon-head", StatKeys.LongestSnake, 60),
        new("giant", "game-icons:anvil", StatKeys.LongestSnake, 150),
        new("high-score", "game-icons:trophy", StatKeys.BestScore, 200),
        new("champion", "game-icons:crown", StatKeys.BestScore, 1000),
        new("treasure-hunter", "game-icons:star-formation", StatKeys.Prizes, 10),
        new("boss-slayer", "game-icons:turret", StatKeys.BossKills, 1),
        new("boss-hunter", "game-icons:cannon", StatKeys.BossKills, 10),
        new("lightning-rod", "game-icons:lightning-storm", StatKeys.LightningHits, 10),
        new("meteor-magnet", "game-icons:meteor-impact", StatKeys.MeteorHits, 10),
    ];
}
