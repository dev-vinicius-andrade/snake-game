using Library.Commons.Game.Domain.Entities;

namespace Application.Game.Server.Stats;

public enum StatsDeliveryOutcome
{
    /// <summary>The manager took the report (or had it already).</summary>
    Accepted,

    /// <summary>The manager will never take it (unknown account, report it refuses): no point sending it again.</summary>
    Refused,

    /// <summary>The manager could not be reached or failed: worth another try.</summary>
    Failed,
}

/// <summary>What became of one sending of a report, and on acceptance the achievements it unlocked.</summary>
public sealed record StatsDelivery(StatsDeliveryOutcome Outcome, IReadOnlyList<string> Unlocked)
{
    public static StatsDelivery Refused { get; } = new(StatsDeliveryOutcome.Refused, []);

    public static StatsDelivery Failed { get; } = new(StatsDeliveryOutcome.Failed, []);

    public static StatsDelivery Accepted(IReadOnlyList<string> unlocked) => new(StatsDeliveryOutcome.Accepted, unlocked);
}

/// <summary>Sends one report to the manager: <c>POST /accounts/{id}/stats</c> (docs/ACHIEVEMENTS.md). Never throws but on cancellation.</summary>
public interface IAccountStatsClient
{
    Task<StatsDelivery> SendAsync(Guid accountId, LifeStatsReport report, CancellationToken cancellationToken = default);
}

/// <summary>Tells a signed-in player what its account just unlocked. Players already gone are skipped.</summary>
public interface IAchievementsNotifier
{
    Task NotifyAsync(Guid accountId, IReadOnlyList<string> achievementIds, CancellationToken cancellationToken = default);
}

/// <summary>How reports wait and are tried again.</summary>
public sealed record StatsReporterOptions
{
    /// <summary>Reports waiting at most; a report beyond is dropped (and logged) so the game loop never waits.</summary>
    public int Capacity { get; init; } = 1_000;

    /// <summary>Sendings of one report at most before it is dropped (and logged).</summary>
    public int MaxAttempts { get; init; } = 5;

    public TimeSpan FirstRetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan MaxRetryDelay { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>The wait before sending attempt number <paramref name="attempt"/> (the first is 1, sent at once): doubling, capped.</summary>
    public TimeSpan DelayBefore(int attempt)
    {
        if (attempt <= 1)
            return TimeSpan.Zero;
        var doubled = FirstRetryDelay.TotalMilliseconds * Math.Pow(2, attempt - 2);
        return TimeSpan.FromMilliseconds(Math.Min(doubled, MaxRetryDelay.TotalMilliseconds));
    }
}
