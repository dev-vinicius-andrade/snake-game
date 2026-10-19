using Library.Commons.Game.Domain.Entities;

namespace Application.Manager.Api.Accounts;

/// <summary>
/// What the manager keeps for one account over all its lives (docs/ACHIEVEMENTS.md): the counters added up, the highest maxima, the last
/// reports already applied (so a report sent twice counts once) and the achievements unlocked with when. Immutable: a change is a new instance.
/// </summary>
public sealed record AccountStats
{
    /// <summary>Report ids remembered per account; a report older than that would count again.</summary>
    public const int RememberedReports = 200;

    private AccountStats(IReadOnlyDictionary<string, long> counters, IReadOnlyDictionary<string, long> maxima, IReadOnlyList<Guid> appliedReports,
        IReadOnlyDictionary<string, DateTimeOffset> unlocked)
    {
        Counters = counters;
        Maxima = maxima;
        AppliedReports = appliedReports;
        Unlocked = unlocked;
    }

    public static AccountStats Empty { get; } = Rehydrate(null, null, null, null);

    /// <summary>Counter totals by <see cref="StatKeys"/> key; a key never reported is missing.</summary>
    public IReadOnlyDictionary<string, long> Counters { get; }

    /// <summary>Highest value ever reported by <see cref="StatKeys"/> key; a key never reported is missing.</summary>
    public IReadOnlyDictionary<string, long> Maxima { get; }

    /// <summary>The ids of the last <see cref="RememberedReports"/> reports applied, oldest first.</summary>
    public IReadOnlyList<Guid> AppliedReports { get; }

    /// <summary>When each achievement was unlocked, by achievement id.</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> Unlocked { get; private init; }

    /// <summary>Stats as they were stored; missing collections are empty and only the last <see cref="RememberedReports"/> report ids are kept.</summary>
    public static AccountStats Rehydrate(IReadOnlyDictionary<string, long>? counters, IReadOnlyDictionary<string, long>? maxima,
        IEnumerable<Guid>? appliedReports, IReadOnlyDictionary<string, DateTimeOffset>? unlocked)
        => new(Copy(counters), Copy(maxima), (appliedReports ?? []).TakeLast(RememberedReports).ToList().AsReadOnly(), Copy(unlocked));

    /// <summary>The value of a counter or a maximum; 0 when never reported.</summary>
    public long ValueOf(string stat) => Counters.TryGetValue(stat, out var counted) ? counted : Maxima.GetValueOrDefault(stat);

    public bool HasApplied(Guid reportId) => AppliedReports.Contains(reportId);

    /// <summary>
    /// These stats with the report added: counters summed, maxima kept at the highest, the report id remembered, and every definition not yet
    /// unlocked whose stat now reaches its threshold unlocked at <paramref name="now"/>.
    /// </summary>
    public StatsChange With(StatsReport report, DateTimeOffset now, IReadOnlyList<AchievementDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(definitions);
        var counted = new AccountStats(Summed(Counters, report.Counters), Highest(Maxima, report.Maxima), Remembered(AppliedReports, report.Id), Unlocked);
        var unlocked = counted.NewlyReached(definitions);
        return new StatsChange(counted with { Unlocked = WithUnlocks(Unlocked, unlocked, now) }, unlocked);
    }

    private IReadOnlyList<string> NewlyReached(IReadOnlyList<AchievementDefinition> definitions)
        => definitions
            .Where(definition => !Unlocked.ContainsKey(definition.Id) && ValueOf(definition.Stat) >= definition.Threshold)
            .Select(definition => definition.Id)
            .Distinct(StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();

    private static IReadOnlyDictionary<string, long> Summed(IReadOnlyDictionary<string, long> totals, IReadOnlyDictionary<string, long> added)
        => Merged(totals, added, (total, value) => total + value);

    private static IReadOnlyDictionary<string, long> Highest(IReadOnlyDictionary<string, long> best, IReadOnlyDictionary<string, long> reported)
        => Merged(best, reported, Math.Max);

    private static IReadOnlyDictionary<string, long> Merged(IReadOnlyDictionary<string, long> current, IReadOnlyDictionary<string, long> reported,
        Func<long, long, long> combine)
    {
        var merged = new Dictionary<string, long>(current, StringComparer.Ordinal);
        foreach (var (key, value) in reported)
            merged[key] = merged.TryGetValue(key, out var existing) ? combine(existing, value) : value;
        return merged.AsReadOnly();
    }

    private static IReadOnlyList<Guid> Remembered(IReadOnlyList<Guid> applied, Guid reportId)
        => applied.Append(reportId).TakeLast(RememberedReports).ToList().AsReadOnly();

    private static IReadOnlyDictionary<string, DateTimeOffset> WithUnlocks(IReadOnlyDictionary<string, DateTimeOffset> unlocked, IEnumerable<string> ids,
        DateTimeOffset now)
        => unlocked.Concat(ids.Select(id => KeyValuePair.Create(id, now))).ToDictionary(StringComparer.Ordinal).AsReadOnly();

    private static IReadOnlyDictionary<string, TValue> Copy<TValue>(IReadOnlyDictionary<string, TValue>? source)
        => (source ?? new Dictionary<string, TValue>()).ToDictionary(StringComparer.Ordinal).AsReadOnly();
}

/// <summary>Stats after a report, and the ids of the achievements that report unlocked, in the order of the definitions.</summary>
public sealed record StatsChange(AccountStats Stats, IReadOnlyList<string> Unlocked);

/// <summary>
/// The numbers a game server saw in one life, with only the keys this manager knows: counters among <see cref="StatKeys.Counters"/>, maxima
/// among <see cref="StatKeys.Maxima"/>. Keys it does not know are dropped whatever their value, so a newer game server never breaks an older manager.
/// </summary>
public sealed record StatsReport
{
    /// <summary>The most one report may add to a counter.</summary>
    public const long MaxCounter = 1_000_000;

    /// <summary>The highest maximum a report may carry.</summary>
    public const long MaxMaximum = 10_000_000;

    private StatsReport(Guid id, IReadOnlyDictionary<string, long> counters, IReadOnlyDictionary<string, long> maxima)
    {
        Id = id;
        Counters = counters;
        Maxima = maxima;
    }

    public Guid Id { get; }

    public IReadOnlyDictionary<string, long> Counters { get; }

    public IReadOnlyDictionary<string, long> Maxima { get; }

    /// <summary>
    /// The report, or null when it must be refused as a whole: an empty id, a known counter outside 0 to <see cref="MaxCounter"/>, or a known
    /// maximum outside 0 to <see cref="MaxMaximum"/>.
    /// </summary>
    public static StatsReport? TryCreate(Guid id, IReadOnlyDictionary<string, long>? counters, IReadOnlyDictionary<string, long>? maxima)
    {
        var knownCounters = Known(counters, StatKeys.IsCounter);
        var knownMaxima = Known(maxima, StatKeys.IsMaximum);
        if (id == Guid.Empty || !AllWithin(knownCounters, MaxCounter) || !AllWithin(knownMaxima, MaxMaximum))
            return null;
        return new StatsReport(id, knownCounters, knownMaxima);
    }

    private static IReadOnlyDictionary<string, long> Known(IReadOnlyDictionary<string, long>? values, Func<string, bool> isKnown)
        => (values ?? new Dictionary<string, long>()).Where(entry => isKnown(entry.Key)).ToDictionary(StringComparer.Ordinal).AsReadOnly();

    private static bool AllWithin(IReadOnlyDictionary<string, long> values, long highest) => values.Values.All(value => value >= 0 && value <= highest);
}

public enum StatsApplyStatus
{
    /// <summary>The report was added; <see cref="StatsApplyResult.Unlocked"/> lists what it unlocked.</summary>
    Applied,

    /// <summary>A report with that id was applied before; nothing changed.</summary>
    AlreadyApplied,

    /// <summary>The report was refused as a whole (see <see cref="StatsReport.TryCreate"/>); nothing changed.</summary>
    Rejected,
}

/// <summary>What applying a report did, and the ids of the achievements it unlocked (empty unless <see cref="StatsApplyStatus.Applied"/>).</summary>
public sealed record StatsApplyResult
{
    private StatsApplyResult(StatsApplyStatus status, IReadOnlyList<string> unlocked)
    {
        Status = status;
        Unlocked = unlocked;
    }

    public static StatsApplyResult AlreadyApplied { get; } = new(StatsApplyStatus.AlreadyApplied, []);

    public static StatsApplyResult Rejected { get; } = new(StatsApplyStatus.Rejected, []);

    public StatsApplyStatus Status { get; }

    public IReadOnlyList<string> Unlocked { get; }

    public static StatsApplyResult Applied(IReadOnlyList<string> unlocked)
    {
        ArgumentNullException.ThrowIfNull(unlocked);
        return new(StatsApplyStatus.Applied, unlocked);
    }
}

/// <summary>Where stats and unlocks are kept. A JSON file for now (FileStatsStore), Postgres later: the rest of the code only knows this.</summary>
public interface IStatsStore
{
    /// <summary>The stats of the account; <see cref="AccountStats.Empty"/> when it never had a report.</summary>
    AccountStats Get(Guid accountId);

    /// <summary>
    /// In one atomic step: refuses an invalid report, acknowledges a report id already applied without applying it again, else adds the report
    /// and unlocks every definition it makes reachable. Throws when the change cannot be saved, and then nothing changes.
    /// </summary>
    StatsApplyResult Apply(Guid accountId, Guid reportId, IReadOnlyDictionary<string, long>? counters, IReadOnlyDictionary<string, long>? maxima,
        DateTimeOffset now, IReadOnlyList<AchievementDefinition> definitions);
}
