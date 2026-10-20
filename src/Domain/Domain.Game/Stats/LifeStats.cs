using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Enums;

namespace Domain.Game.Stats;

/// <summary>
/// What one life of a signed-in snake did, counted while it plays and turned into a <see cref="LifeStatsReport"/> when it ends
/// (docs/ACHIEVEMENTS.md). Kept by the game loop of the snake's room only, so it is not thread safe.
/// </summary>
public sealed class LifeStats
{
    /// <summary>The most a report may carry for one key; the manager refuses more.</summary>
    public const long MaxReportedValue = 1_000_000;

    private readonly DateTimeOffset _startedAt;
    private bool _died;
    private long _kills;
    private long _foods;
    private long _prizes;
    private long _bossKills;
    private long _lightningHits;
    private long _meteorHits;
    private long _bestScore;
    private long _longestSnake;
    private long _shotsFired;
    private long _shotsHit;
    private double _damageDealt;
    private long _segmentsBroken;
    private TimeSpan _boosted;

    private LifeStats(DateTimeOffset startedAt) => _startedAt = startedAt;

    /// <summary>A life that begins now, with the snake at this mass and number of segments.</summary>
    public static LifeStats Begin(DateTimeOffset startedAt, long score, int length)
    {
        var stats = new LifeStats(startedAt);
        stats.Observe(score, length);
        return stats;
    }

    /// <summary>The snake ate something from the map: one food, whatever it credits toward perks; a prize is also counted apart.</summary>
    public void Ate(FoodKind kind)
    {
        _foods++;
        if (kind == FoodKind.Prize) _prizes++;
    }

    public void Killed() => _kills++;

    /// <summary>One shot left the snake (every shot of a volley counts).</summary>
    public void ShotFired() => _shotsFired++;

    /// <summary>One of its shots hit another snake, and took this much off it (nothing for a hit on the head, which kills).</summary>
    public void ShotHit(double damage)
    {
        _shotsHit++;
        _damageDealt += Math.Max(0, damage);
    }

    /// <summary>One of its shots broke a body segment off another snake.</summary>
    public void SegmentBroken() => _segmentsBroken++;

    /// <summary>The snake spent this long boosting.</summary>
    public void Boosted(TimeSpan time) => _boosted += time;

    /// <summary>The snake landed the last shot on the boss.</summary>
    public void BroughtDownBoss() => _bossKills++;

    /// <summary>The snake lost mass to a strike of this kind.</summary>
    public void Struck(WorldStrikeKind kind)
    {
        if (kind == WorldStrikeKind.Meteor) _meteorHits++;
        else _lightningHits++;
    }

    public void Died() => _died = true;

    /// <summary>Keeps the highest mass and number of segments seen in this life.</summary>
    public void Observe(long score, int length)
    {
        _bestScore = Math.Max(_bestScore, score);
        _longestSnake = Math.Max(_longestSnake, length);
    }

    /// <summary>The report of the life, ended at <paramref name="endedAt"/>. Every value is between 0 and <see cref="MaxReportedValue"/>.</summary>
    public LifeStatsReport Finish(DateTimeOffset endedAt, Guid reportId)
    {
        var counters = new Dictionary<string, long>
        {
            [StatKeys.Games] = 1,
            [StatKeys.Deaths] = _died ? 1 : 0,
            [StatKeys.Kills] = _kills,
            [StatKeys.Foods] = _foods,
            [StatKeys.Prizes] = _prizes,
            [StatKeys.BossKills] = _bossKills,
            [StatKeys.LightningHits] = _lightningHits,
            [StatKeys.MeteorHits] = _meteorHits,
            [StatKeys.PlaySeconds] = (long)(endedAt - _startedAt).TotalSeconds,
            [StatKeys.ShotsFired] = _shotsFired,
            [StatKeys.ShotsHit] = _shotsHit,
            [StatKeys.DamageDealt] = (long)_damageDealt,
            [StatKeys.SegmentsBroken] = _segmentsBroken,
            [StatKeys.BoostSeconds] = (long)_boosted.TotalSeconds,
        };
        var maxima = new Dictionary<string, long>
        {
            [StatKeys.BestScore] = _bestScore,
            [StatKeys.LongestSnake] = _longestSnake,
            [StatKeys.BestKillsInLife] = _kills,
            [StatKeys.LongestLife] = (long)(endedAt - _startedAt).TotalSeconds,
            [StatKeys.BestFoodsInLife] = _foods,
        };
        return new LifeStatsReport(reportId, Capped(counters), Capped(maxima));
    }

    private static IReadOnlyDictionary<string, long> Capped(Dictionary<string, long> values)
        => values.ToDictionary(pair => pair.Key, pair => Math.Clamp(pair.Value, 0, MaxReportedValue)).AsReadOnly();
}
