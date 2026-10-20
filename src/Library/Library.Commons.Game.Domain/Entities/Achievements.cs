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
    public const string ShotsFired = "shotsFired";
    public const string ShotsHit = "shotsHit";
    public const string DamageDealt = "damageDealt";
    public const string SegmentsBroken = "segmentsBroken";
    public const string BoostSeconds = "boostSeconds";

    // Maxima: a game server reports the best of one life, the manager keeps the highest ever.
    public const string BestScore = "bestScore";
    public const string LongestSnake = "longestSnake";
    public const string BestKillsInLife = "bestKillsInLife";
    public const string LongestLife = "longestLife";
    public const string BestFoodsInLife = "bestFoodsInLife";

    public static IReadOnlyList<string> Counters { get; } =
        [Games, Deaths, Kills, Foods, Prizes, BossKills, LightningHits, MeteorHits, PlaySeconds, ShotsFired, ShotsHit, DamageDealt, SegmentsBroken, BoostSeconds];

    public static IReadOnlyList<string> Maxima { get; } = [BestScore, LongestSnake, BestKillsInLife, LongestLife, BestFoodsInLife];

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
        new("veteran", "game-icons:stopwatch", StatKeys.PlaySeconds, 36000),
        new("hooked", "game-icons:cycle", StatKeys.Games, 100),
        new("apex", "game-icons:rune-sword", StatKeys.Kills, 500),
        new("fruit-fanatic", "game-icons:triple-corn", StatKeys.Foods, 5000),
        new("never-give-up", "game-icons:broken-skull", StatKeys.Deaths, 50),
        new("legend", "game-icons:diamond-hard", StatKeys.BestScore, 5000),
        new("world-eater", "game-icons:whirlwind", StatKeys.LongestSnake, 300),
        new("prize-hoarder", "game-icons:heart-plus", StatKeys.Prizes, 50),
        new("turret-terror", "game-icons:laser-blast", StatKeys.BossKills, 50),
        new("trigger-happy", "game-icons:bullets", StatKeys.ShotsFired, 100),
        new("bullet-storm", "game-icons:machine-gun-magazine", StatKeys.ShotsFired, 2000),
        new("sharpshooter", "game-icons:bullseye", StatKeys.ShotsHit, 25),
        new("dead-eye", "game-icons:target-arrows", StatKeys.ShotsHit, 300),
        new("wrecking-ball", "game-icons:falling-bomb", StatKeys.DamageDealt, 500),
        new("demolition", "game-icons:carpet-bombing", StatKeys.DamageDealt, 5000),
        new("chop-chop", "game-icons:sword-brandish", StatKeys.SegmentsBroken, 10),
        new("butcher", "game-icons:cluster-bomb", StatKeys.SegmentsBroken, 100),
        new("speed-demon", "game-icons:sprint", StatKeys.BoostSeconds, 300),
        new("afterburner", "game-icons:rocket-flight", StatKeys.BoostSeconds, 3600),
        new("survivor", "game-icons:shield", StatKeys.LongestLife, 300),
        new("ghost-snake", "game-icons:ghost", StatKeys.LongestLife, 900),
        new("all-you-can-eat", "game-icons:health-increase", StatKeys.BestFoodsInLife, 100),
        new("bottomless", "game-icons:lungs", StatKeys.BestFoodsInLife, 400),
    ];
}
