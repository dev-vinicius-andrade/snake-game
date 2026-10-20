using Application.Manager.Api.Accounts;
using Library.Commons.Game.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Manager.Api.Persistence;

/// <summary>
/// Stats and unlocks in the database. A report is applied in one transaction: the report id is remembered, the counters and maxima changed and
/// the unlocks added together, or none of it. The account's <see cref="StatsHeadRow.Version"/> is a concurrency token, so when two reports for
/// one account run at once (on two manager instances, say) the one that loses starts again from what the winner saved, and the numbers add up
/// exactly. Throws when the change cannot be saved, and then nothing changed.
/// </summary>
public sealed class EfStatsStore(IDbContextFactory<SnakeDbContext> factory) : IStatsStore
{
    private const int Attempts = 8;

    public AccountStats Get(Guid accountId)
    {
        using var context = factory.CreateDbContext();
        return Load(context, accountId).Stats;
    }

    public StatsApplyResult Apply(Guid accountId, Guid reportId, IReadOnlyDictionary<string, long>? counters, IReadOnlyDictionary<string, long>? maxima,
        DateTimeOffset now, IReadOnlyList<AchievementDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var report = StatsReport.TryCreate(reportId, counters, maxima);
        if (report is null)
            return StatsApplyResult.Rejected;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return ApplyOnce(accountId, report, now, definitions);
            }
            catch (Exception exception) when (attempt < Attempts && exception is DbUpdateException)
            {
                // Another report for this account was saved first (a stale version, or the same row added twice): look again at what it left.
            }
        }
    }

    private StatsApplyResult ApplyOnce(Guid accountId, StatsReport report, DateTimeOffset now, IReadOnlyList<AchievementDefinition> definitions)
    {
        using var context = factory.CreateDbContext();
        var loaded = Load(context, accountId);
        if (loaded.Stats.HasApplied(report.Id))
            return StatsApplyResult.AlreadyApplied;
        var change = loaded.Stats.With(report, now, definitions);
        Write(context, accountId, loaded, change, report.Id, now);
        context.SaveChanges();
        return StatsApplyResult.Applied(change.Unlocked);
    }

    private static Loaded Load(SnakeDbContext context, Guid accountId)
    {
        var head = context.StatsHeads.SingleOrDefault(row => row.AccountId == accountId);
        var values = context.StatValues.Where(row => row.AccountId == accountId).ToList();
        var reports = context.AppliedReports.Where(row => row.AccountId == accountId).OrderBy(row => row.Sequence).ToList();
        var unlocked = context.UnlockedAchievements.Where(row => row.AccountId == accountId).ToList();
        var stats = AccountStats.Rehydrate(
            values.Where(row => row.Kind == StatKind.Counter).ToDictionary(row => row.Key, row => row.Value),
            values.Where(row => row.Kind == StatKind.Maximum).ToDictionary(row => row.Key, row => row.Value),
            reports.Select(row => row.ReportId),
            unlocked.ToDictionary(row => row.AchievementId, row => row.UnlockedAt));
        return new Loaded(stats, head, values, reports);
    }

    private static void Write(SnakeDbContext context, Guid accountId, Loaded loaded, StatsChange change, Guid reportId, DateTimeOffset now)
    {
        if (loaded.Head is null) context.StatsHeads.Add(new StatsHeadRow { AccountId = accountId, Version = 1 });
        else loaded.Head.Version++;
        WriteValues(context, accountId, loaded.Values, StatKind.Counter, change.Stats.Counters);
        WriteValues(context, accountId, loaded.Values, StatKind.Maximum, change.Stats.Maxima);
        WriteReport(context, accountId, loaded.Reports, reportId);
        foreach (var id in change.Unlocked)
            context.UnlockedAchievements.Add(new UnlockedAchievementRow { AccountId = accountId, AchievementId = id, UnlockedAt = now });
    }

    private static void WriteValues(SnakeDbContext context, Guid accountId, List<StatValueRow> rows, StatKind kind, IReadOnlyDictionary<string, long> values)
    {
        var existing = rows.Where(row => row.Kind == kind).ToDictionary(row => row.Key, StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            if (!existing.TryGetValue(key, out var row)) context.StatValues.Add(new StatValueRow { AccountId = accountId, Kind = kind, Key = key, Value = value });
            else if (row.Value != value) row.Value = value;
        }
    }

    private static void WriteReport(SnakeDbContext context, Guid accountId, List<AppliedReportRow> reports, Guid reportId)
    {
        var next = reports.Count == 0 ? 1 : reports[^1].Sequence + 1;
        context.AppliedReports.Add(new AppliedReportRow { AccountId = accountId, ReportId = reportId, Sequence = next });
        var surplus = reports.Count + 1 - AccountStats.RememberedReports;
        if (surplus > 0) context.AppliedReports.RemoveRange(reports.Take(surplus));
    }

    private sealed record Loaded(AccountStats Stats, StatsHeadRow? Head, List<StatValueRow> Values, List<AppliedReportRow> Reports);
}
