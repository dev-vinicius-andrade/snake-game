using Application.Manager.Api.Accounts;
using Library.Commons.Game.Domain.Entities;

namespace Application.Manager.Api.Tests.Persistence;

public sealed class EfStatsStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 19, 12, 34, 56, TimeSpan.FromHours(-3)).AddTicks(1_234_560);

    private static readonly IReadOnlyList<AchievementDefinition> Definitions =
    [
        new("first-blood", "game-icons:pointy-sword", StatKeys.Kills, 1),
        new("hunter", "game-icons:crosshair", StatKeys.Kills, 25),
        new("high-score", "game-icons:trophy", StatKeys.BestScore, 200),
        new("first-steps", "game-icons:snake", StatKeys.Games, 1),
    ];

    private static readonly Dictionary<string, long> NoStats = new();

    private readonly PersistenceTestHost _host = new();
    private readonly Guid _account = Guid.NewGuid();

    public void Dispose() => _host.Dispose();

    private static Dictionary<string, long> Stats(params (string Key, long Value)[] values) => values.ToDictionary(value => value.Key, value => value.Value);

    private StatsApplyResult Report(IStatsStore store, Dictionary<string, long>? counters = null, Dictionary<string, long>? maxima = null,
        Guid? reportId = null, DateTimeOffset? now = null, Guid? account = null)
        => store.Apply(account ?? _account, reportId ?? Guid.NewGuid(), counters ?? NoStats, maxima ?? NoStats, now ?? Now, Definitions);

    [Fact]
    public void Get_AnAccountWithoutReports_IsEmpty()
    {
        var stats = _host.Stats().Get(_account);

        Assert.Empty(stats.Counters);
        Assert.Empty(stats.Maxima);
        Assert.Empty(stats.Unlocked);
        Assert.Empty(stats.AppliedReports);
    }

    [Fact]
    public void Apply_CountersOfSeveralReportsAddUp_AndMaximaKeepTheHighest()
    {
        var store = _host.Stats();
        Report(store, Stats((StatKeys.Kills, 2)), Stats((StatKeys.BestScore, 50)));
        Report(store, Stats((StatKeys.Kills, 3)), Stats((StatKeys.BestScore, 30)));

        var stats = store.Get(_account);

        Assert.Equal(5, stats.Counters[StatKeys.Kills]);
        Assert.Equal(50, stats.Maxima[StatKeys.BestScore]);
    }

    [Fact]
    public void Apply_IsKeptAndComesBackExactlyInANewStore()
    {
        Report(_host.Stats(), Stats((StatKeys.Kills, 30), (StatKeys.Games, 1)), Stats((StatKeys.BestScore, 250)));

        var stats = _host.Stats().Get(_account);

        Assert.Equal(30, stats.Counters[StatKeys.Kills]);
        Assert.Equal(250, stats.Maxima[StatKeys.BestScore]);
        Assert.Equal(new[] { "first-blood", "first-steps", "high-score", "hunter" }, stats.Unlocked.Keys.Order());
        Assert.Equal(Now.UtcDateTime, stats.Unlocked["hunter"].UtcDateTime);
        Assert.Single(stats.AppliedReports);
    }

    [Fact]
    public void Apply_AccountsAreKeptApart()
    {
        var store = _host.Stats();
        var other = Guid.NewGuid();
        Report(store, Stats((StatKeys.Kills, 1)));
        Report(store, Stats((StatKeys.Kills, 7)), account: other);

        Assert.Equal(1, store.Get(_account).Counters[StatKeys.Kills]);
        Assert.Equal(7, store.Get(other).Counters[StatKeys.Kills]);
    }

    [Fact]
    public void Apply_CrossingAThreshold_UnlocksItOnceAtThatReport()
    {
        var store = _host.Stats();

        var first = Report(store, Stats((StatKeys.Kills, 24)));
        var second = Report(store, Stats((StatKeys.Kills, 1)), now: Now.AddHours(1));
        var third = Report(store, Stats((StatKeys.Kills, 1)), now: Now.AddHours(2));

        Assert.Equal(new[] { "first-blood" }, first.Unlocked);
        Assert.Equal(new[] { "hunter" }, second.Unlocked);
        Assert.Empty(third.Unlocked);
        Assert.Equal(Now.AddHours(1).UtcDateTime, store.Get(_account).Unlocked["hunter"].UtcDateTime);
    }

    [Fact]
    public void Apply_OneReportReachingSeveralThresholds_UnlocksThemAllInTheOrderOfTheDefinitions()
    {
        var result = Report(_host.Stats(), Stats((StatKeys.Kills, 30), (StatKeys.Games, 1)), Stats((StatKeys.BestScore, 300)));

        Assert.Equal(new[] { "first-blood", "hunter", "high-score", "first-steps" }, result.Unlocked);
    }

    [Fact]
    public void Apply_TheSameReportIdTwice_IsAppliedOnceAndTheSecondUnlocksNothing()
    {
        var store = _host.Stats();
        var reportId = Guid.NewGuid();
        Report(store, Stats((StatKeys.Kills, 1)), reportId: reportId);

        var again = Report(store, Stats((StatKeys.Kills, 30)), reportId: reportId);

        Assert.Equal(StatsApplyStatus.AlreadyApplied, again.Status);
        Assert.Empty(again.Unlocked);
        Assert.Equal(1, store.Get(_account).Counters[StatKeys.Kills]);
    }

    [Fact]
    public void Apply_TheSameReportIdForAnotherAccount_IsAnotherReport()
    {
        var store = _host.Stats();
        var reportId = Guid.NewGuid();
        Report(store, Stats((StatKeys.Kills, 1)), reportId: reportId);

        Assert.Equal(StatsApplyStatus.Applied, Report(store, Stats((StatKeys.Kills, 1)), reportId: reportId, account: Guid.NewGuid()).Status);
    }

    [Fact]
    public void Apply_RemembersTheLast200ReportIdsOfAnAccount_InOrder()
    {
        var store = _host.Stats();
        var ids = Enumerable.Range(0, AccountStats.RememberedReports + 1).Select(_ => Guid.NewGuid()).ToList();
        ids.ForEach(id => Report(store, Stats((StatKeys.Games, 1)), reportId: id));

        var forgotten = Report(store, Stats((StatKeys.Games, 1)), reportId: ids[0]);
        var remembered = Report(store, Stats((StatKeys.Games, 1)), reportId: ids[^1]);

        Assert.Equal(StatsApplyStatus.Applied, forgotten.Status);
        Assert.Equal(StatsApplyStatus.AlreadyApplied, remembered.Status);
        Assert.Equal(AccountStats.RememberedReports, store.Get(_account).AppliedReports.Count);
        Assert.Equal(ids[0], store.Get(_account).AppliedReports[^1]);
    }

    [Fact]
    public void Apply_UnknownKeys_AreIgnored_AndAnAbsurdValueRefusesTheWholeReport()
    {
        var store = _host.Stats();

        var ignored = Report(store, Stats(("made-up", 5), (StatKeys.Kills, 1)));
        var refused = Report(store, Stats((StatKeys.Kills, -1)));

        Assert.Equal(StatsApplyStatus.Applied, ignored.Status);
        Assert.Equal(StatsApplyStatus.Rejected, refused.Status);
        Assert.False(store.Get(_account).Counters.ContainsKey("made-up"));
        Assert.Equal(1, store.Get(_account).Counters[StatKeys.Kills]);
    }

    [Fact]
    public void Apply_AnEmptyReportId_IsRefused_AndAReportWithoutNumbersStillCounts()
    {
        var store = _host.Stats();

        Assert.Equal(StatsApplyStatus.Rejected, Report(store, reportId: Guid.Empty).Status);
        Assert.Equal(StatsApplyStatus.Applied, Report(store).Status);
        Assert.Single(store.Get(_account).AppliedReports);
    }

    [Fact]
    public void Apply_ADefinitionAlreadyUnlocked_IsNotUnlockedAgainWhenTheListChanges()
    {
        var store = _host.Stats();
        Report(store, Stats((StatKeys.Kills, 1)));

        var result = store.Apply(_account, Guid.NewGuid(), Stats((StatKeys.Kills, 1)), NoStats, Now.AddDays(1), Definitions.Reverse().ToList());

        Assert.Empty(result.Unlocked);
        Assert.Equal(Now.UtcDateTime, store.Get(_account).Unlocked["first-blood"].UtcDateTime);
    }

    [Fact]
    public void Get_ReturnsASnapshotThatLaterReportsDoNotTouch()
    {
        var store = _host.Stats();
        Report(store, Stats((StatKeys.Kills, 1)));
        var before = store.Get(_account);

        Report(store, Stats((StatKeys.Kills, 5)));

        Assert.Equal(1, before.Counters[StatKeys.Kills]);
    }
}
