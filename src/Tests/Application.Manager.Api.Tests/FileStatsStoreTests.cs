using System.Collections.Concurrent;
using Application.Manager.Api.Accounts;
using Library.Commons.Game.Domain.Entities;

namespace Application.Manager.Api.Tests;

public sealed class FileStatsStoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new DateTimeOffset(2026, 10, 19, 12, 34, 56, TimeSpan.FromHours(-3)).AddTicks(1_234_567);

    private static readonly IReadOnlyList<AchievementDefinition> Definitions =
    [
        new("first-blood", "game-icons:pointy-sword", StatKeys.Kills, 1),
        new("hunter", "game-icons:crosshair", StatKeys.Kills, 25),
        new("high-score", "game-icons:trophy", StatKeys.BestScore, 200),
        new("first-steps", "game-icons:snake", StatKeys.Games, 1),
    ];

    private static readonly Dictionary<string, long> NoStats = new();

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "snake-stats-" + Guid.NewGuid().ToString("N"));
    private readonly Guid _account = Guid.NewGuid();

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private string StorePath => Path.Combine(_folder, "stats.json");

    private FileStatsStore FileStore() => new(StorePath);

    private static FileStatsStore MemoryStore() => new(null);

    private static Dictionary<string, long> Stats(params (string Key, long Value)[] values) => values.ToDictionary(value => value.Key, value => value.Value);

    private StatsApplyResult Report(IStatsStore store, Dictionary<string, long>? counters = null, Dictionary<string, long>? maxima = null,
        Guid? reportId = null, DateTimeOffset? now = null, Guid? account = null)
        => store.Apply(account ?? _account, reportId ?? Guid.NewGuid(), counters ?? NoStats, maxima ?? NoStats, now ?? Now, Definitions);

    [Fact]
    public void Get_AnAccountWithoutReports_IsEmpty()
    {
        var stats = MemoryStore().Get(_account);

        Assert.Empty(stats.Counters);
        Assert.Empty(stats.Maxima);
        Assert.Empty(stats.AppliedReports);
        Assert.Empty(stats.Unlocked);
    }

    [Fact]
    public void Apply_CountersOfSeveralReports_AddUp()
    {
        var store = MemoryStore();
        Report(store, Stats((StatKeys.Kills, 2), (StatKeys.Foods, 10)));
        Report(store, Stats((StatKeys.Kills, 3)));

        var stats = store.Get(_account);

        Assert.Equal(5, stats.Counters[StatKeys.Kills]);
        Assert.Equal(10, stats.Counters[StatKeys.Foods]);
    }

    [Fact]
    public void Apply_Maxima_KeepTheHighestEverReported()
    {
        var store = MemoryStore();
        Report(store, maxima: Stats((StatKeys.BestScore, 120), (StatKeys.LongestSnake, 30)));
        Report(store, maxima: Stats((StatKeys.BestScore, 80), (StatKeys.LongestSnake, 45)));

        var stats = store.Get(_account);

        Assert.Equal(120, stats.Maxima[StatKeys.BestScore]);
        Assert.Equal(45, stats.Maxima[StatKeys.LongestSnake]);
    }

    [Fact]
    public void Apply_AccountsAreKeptApart()
    {
        var store = MemoryStore();
        var other = Guid.NewGuid();
        Report(store, Stats((StatKeys.Kills, 2)));

        Report(store, Stats((StatKeys.Kills, 7)), account: other);

        Assert.Equal(2, store.Get(_account).Counters[StatKeys.Kills]);
        Assert.Equal(7, store.Get(other).Counters[StatKeys.Kills]);
    }

    [Fact]
    public void Apply_CrossingAThreshold_UnlocksItOnceAtThatReport()
    {
        var store = MemoryStore();
        var crossing = Now.AddMinutes(5);
        var below = Report(store, Stats((StatKeys.Kills, 24)));

        var crossed = Report(store, Stats((StatKeys.Kills, 1)), now: crossing);
        var above = Report(store, Stats((StatKeys.Kills, 30)), now: crossing.AddMinutes(5));

        Assert.Equal(new[] { "first-blood" }, below.Unlocked);
        Assert.Equal(new[] { "hunter" }, crossed.Unlocked);
        Assert.Empty(above.Unlocked);
        Assert.Equal(StatsApplyStatus.Applied, above.Status);
        Assert.Equal(crossing, store.Get(_account).Unlocked["hunter"]);
        Assert.Equal(Now, store.Get(_account).Unlocked["first-blood"]);
    }

    [Fact]
    public void Apply_OneReportReachingSeveralThresholds_UnlocksThemAllInTheOrderOfTheDefinitions()
    {
        var store = MemoryStore();

        var result = Report(store, Stats((StatKeys.Games, 1), (StatKeys.Kills, 30)), Stats((StatKeys.BestScore, 250)));

        Assert.Equal(StatsApplyStatus.Applied, result.Status);
        Assert.Equal(new[] { "first-blood", "hunter", "high-score", "first-steps" }, result.Unlocked);
        Assert.Equal(new[] { "first-blood", "first-steps", "high-score", "hunter" }, store.Get(_account).Unlocked.Keys.Order());
    }

    [Fact]
    public void Apply_AReportBelowEveryThreshold_UnlocksNothing()
    {
        var store = MemoryStore();

        var result = Report(store, Stats((StatKeys.Foods, 3)), Stats((StatKeys.BestScore, 199)));

        Assert.Equal(StatsApplyStatus.Applied, result.Status);
        Assert.Empty(result.Unlocked);
        Assert.Empty(store.Get(_account).Unlocked);
    }

    [Fact]
    public void Apply_TheSameReportIdTwice_IsAppliedOnceAndTheSecondUnlocksNothing()
    {
        var store = MemoryStore();
        var reportId = Guid.NewGuid();
        Report(store, Stats((StatKeys.Kills, 1)), reportId: reportId);

        var again = Report(store, Stats((StatKeys.Kills, 30)), reportId: reportId);

        Assert.Equal(StatsApplyStatus.AlreadyApplied, again.Status);
        Assert.Empty(again.Unlocked);
        Assert.Equal(1, store.Get(_account).Counters[StatKeys.Kills]);
        Assert.False(store.Get(_account).Unlocked.ContainsKey("hunter"));
    }

    [Fact]
    public void Apply_TheSameReportIdForAnotherAccount_IsAnotherReport()
    {
        var store = MemoryStore();
        var reportId = Guid.NewGuid();
        Report(store, Stats((StatKeys.Kills, 1)), reportId: reportId);

        var other = Report(store, Stats((StatKeys.Kills, 1)), reportId: reportId, account: Guid.NewGuid());

        Assert.Equal(StatsApplyStatus.Applied, other.Status);
    }

    [Fact]
    public void Apply_RemembersTheLast200ReportIdsOfAnAccount()
    {
        var store = MemoryStore();
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
    public void Apply_UnknownKeys_AreIgnoredWhateverTheirValue()
    {
        var store = MemoryStore();

        var result = Report(store,
            Stats((StatKeys.Kills, 2), ("jumps", -5), (StatKeys.BestScore, 999)),
            Stats((StatKeys.BestScore, 50), ("fastestLap", 99_999_999), (StatKeys.Kills, 40)));

        var stats = store.Get(_account);
        Assert.Equal(StatsApplyStatus.Applied, result.Status);
        Assert.Equal(new[] { StatKeys.Kills }, stats.Counters.Keys);
        Assert.Equal(2, stats.Counters[StatKeys.Kills]);
        Assert.Equal(new[] { StatKeys.BestScore }, stats.Maxima.Keys);
        Assert.Equal(50, stats.Maxima[StatKeys.BestScore]);
        Assert.Equal(new[] { "first-blood" }, result.Unlocked);
    }

    [Fact]
    public void Apply_KeysInAnotherCase_AreUnknown()
    {
        var store = MemoryStore();

        Report(store, Stats(("Kills", 3)));

        Assert.Empty(store.Get(_account).Counters);
    }

    [Theory]
    [InlineData(StatKeys.Kills, -1, null, 0)]
    [InlineData(StatKeys.PlaySeconds, StatsReport.MaxCounter + 1, null, 0)]
    [InlineData(StatKeys.Kills, long.MaxValue, null, 0)]
    [InlineData(null, 0, StatKeys.BestScore, -1)]
    [InlineData(null, 0, StatKeys.LongestSnake, StatsReport.MaxMaximum + 1)]
    public void Apply_ANegativeOrAbsurdValue_RefusesTheWholeReport(string? counter, long counterValue, string? maximum, long maximumValue)
    {
        var store = MemoryStore();
        var counters = Stats((StatKeys.Games, 1));
        if (counter is not null) counters[counter] = counterValue;
        var maxima = Stats((StatKeys.BestKillsInLife, 3));
        if (maximum is not null) maxima[maximum] = maximumValue;
        var reportId = Guid.NewGuid();

        var result = Report(store, counters, maxima, reportId);

        Assert.Equal(StatsApplyStatus.Rejected, result.Status);
        Assert.Empty(result.Unlocked);
        var stats = store.Get(_account);
        Assert.Empty(stats.Counters);
        Assert.Empty(stats.Maxima);
        Assert.Empty(stats.AppliedReports);
    }

    [Fact]
    public void Apply_TheLimitsThemselves_AreAccepted()
    {
        var store = MemoryStore();

        var result = Report(store, Stats((StatKeys.Kills, StatsReport.MaxCounter), (StatKeys.Deaths, 0)), Stats((StatKeys.BestScore, StatsReport.MaxMaximum), (StatKeys.LongestSnake, 0)));

        Assert.Equal(StatsApplyStatus.Applied, result.Status);
        Assert.Equal(StatsReport.MaxCounter, store.Get(_account).Counters[StatKeys.Kills]);
        Assert.Equal(StatsReport.MaxMaximum, store.Get(_account).Maxima[StatKeys.BestScore]);
    }

    [Fact]
    public void Apply_AnEmptyReportId_IsRefused()
    {
        var store = MemoryStore();

        var result = Report(store, Stats((StatKeys.Kills, 1)), reportId: Guid.Empty);

        Assert.Equal(StatsApplyStatus.Rejected, result.Status);
        Assert.Empty(store.Get(_account).Counters);
    }

    [Fact]
    public void Apply_WithoutCountersOrMaxima_StillCountsAsAppliedReport()
    {
        var store = MemoryStore();
        var reportId = Guid.NewGuid();

        var result = store.Apply(_account, reportId, null, null, Now, Definitions);

        Assert.Equal(StatsApplyStatus.Applied, result.Status);
        Assert.Equal(new[] { reportId }, store.Get(_account).AppliedReports);
    }

    [Fact]
    public void Apply_ADefinitionAlreadyUnlocked_IsNotUnlockedAgainWhenTheListChanges()
    {
        var store = MemoryStore();
        Report(store, Stats((StatKeys.Kills, 1)));

        var result = store.Apply(_account, Guid.NewGuid(), Stats((StatKeys.Kills, 1)), NoStats, Now.AddHours(1),
            [new("first-blood", "icon", StatKeys.Kills, 1), new("double", "icon", StatKeys.Kills, 2)]);

        Assert.Equal(new[] { "double" }, result.Unlocked);
        Assert.Equal(Now, store.Get(_account).Unlocked["first-blood"]);
    }

    [Fact]
    public void Get_ReturnsASnapshotThatLaterReportsDoNotTouch()
    {
        var store = MemoryStore();
        Report(store, Stats((StatKeys.Kills, 1)));
        var before = store.Get(_account);

        Report(store, Stats((StatKeys.Kills, 30)), Stats((StatKeys.BestScore, 10)));

        Assert.Equal(1, before.Counters[StatKeys.Kills]);
        Assert.Empty(before.Maxima);
        Assert.Single(before.AppliedReports);
        Assert.Equal(new[] { "first-blood" }, before.Unlocked.Keys);
    }

    [Fact]
    public void Apply_ChangingTheCallersDictionariesAfterwards_DoesNotChangeTheStore()
    {
        var store = MemoryStore();
        var counters = Stats((StatKeys.Kills, 1));
        Report(store, counters);

        counters[StatKeys.Kills] = 500;

        Assert.Equal(1, store.Get(_account).Counters[StatKeys.Kills]);
    }

    [Fact]
    public void Constructor_WithNoPathOrABlankOne_KeepsStatsInMemoryOnly()
    {
        var withBlank = new FileStatsStore("   ");

        Report(withBlank, Stats((StatKeys.Kills, 1)));

        Assert.Equal(1, withBlank.Get(_account).Counters[StatKeys.Kills]);
        Assert.Empty(new FileStatsStore(null).Get(_account).Counters);
        Assert.False(Directory.Exists(_folder));
    }

    [Fact]
    public void Apply_IsKeptOnDiskAndComesBackExactlyInANewInstance()
    {
        var reportId = Guid.NewGuid();
        Report(FileStore(), Stats((StatKeys.Kills, 3), (StatKeys.Games, 1)), Stats((StatKeys.BestScore, 250)), reportId);

        var reopened = FileStore();

        var stats = reopened.Get(_account);
        Assert.Equal(3, stats.Counters[StatKeys.Kills]);
        Assert.Equal(1, stats.Counters[StatKeys.Games]);
        Assert.Equal(250, stats.Maxima[StatKeys.BestScore]);
        Assert.Equal(new[] { reportId }, stats.AppliedReports);
        Assert.Equal(Now, stats.Unlocked["high-score"]);
        Assert.Equal(Now.Offset, stats.Unlocked["high-score"].Offset);
        Assert.Equal(StatsApplyStatus.AlreadyApplied, Report(reopened, Stats((StatKeys.Kills, 3)), reportId: reportId).Status);
    }

    [Fact]
    public void Apply_WritesIndentedCamelCaseJson()
    {
        Report(FileStore(), Stats((StatKeys.BossKills, 1)), Stats((StatKeys.BestKillsInLife, 2)));

        var text = File.ReadAllText(StorePath);

        Assert.Contains("\"version\": 1", text);
        Assert.Contains("\"accountId\"", text);
        Assert.Contains("\"bossKills\": 1", text);
        Assert.Contains("\"bestKillsInLife\": 2", text);
        Assert.Contains('\n', text);
    }

    [Fact]
    public void Constructor_WhenTheFileDoesNotExist_StartsEmptyWithoutCreatingIt()
    {
        var store = FileStore();

        Assert.Empty(store.Get(_account).Counters);
        Assert.False(File.Exists(StorePath));
    }

    [Theory]
    [InlineData("{ this is not json")]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{ \"version\": 1, \"accounts\": [ null ] }")]
    [InlineData("{ \"version\": 1, \"accounts\": [ { \"accountId\": \"00000000-0000-0000-0000-000000000000\" } ] }")]
    [InlineData("{ \"version\": 1, \"accounts\": [ { \"accountId\": \"6f9619ff-8b86-d011-b42d-00c04fc964ff\" }, { \"accountId\": \"6f9619ff-8b86-d011-b42d-00c04fc964ff\" } ] }")]
    [InlineData("{ \"version\": 1, \"accounts\": [ { \"accountId\": \"6f9619ff-8b86-d011-b42d-00c04fc964ff\", \"counters\": { \"kills\": \"many\" } } ] }")]
    public void Constructor_WithAFileThatDoesNotHoldValidStats_KeepsItAsideAndStartsEmpty(string content)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(StorePath, content);

        var store = FileStore();

        Assert.Empty(store.Get(Guid.Parse("6f9619ff-8b86-d011-b42d-00c04fc964ff")).Counters);
        Assert.False(File.Exists(StorePath));
        var quarantined = Assert.Single(Directory.GetFiles(_folder, "stats.json.corrupt-*"));
        Assert.Equal(content, File.ReadAllText(quarantined));
    }

    [Fact]
    public void Apply_AfterACorruptFileWasKeptAside_WritesANewFileAndLeavesTheBadOneAlone()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(StorePath, "[1, 2,");
        var store = FileStore();

        Report(store, Stats((StatKeys.Kills, 1)));

        var quarantined = Assert.Single(Directory.GetFiles(_folder, "stats.json.corrupt-*"));
        Assert.Equal("[1, 2,", File.ReadAllText(quarantined));
        Assert.Equal(1, FileStore().Get(_account).Counters[StatKeys.Kills]);
    }

    [Fact]
    public void Constructor_AFileWithoutTheOptionalLists_LoadsThemEmpty()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(StorePath, $"{{ \"version\": 1, \"accounts\": [ {{ \"accountId\": \"{_account}\", \"counters\": {{ \"kills\": 4 }} }} ] }}");

        var stats = FileStore().Get(_account);

        Assert.Equal(4, stats.Counters[StatKeys.Kills]);
        Assert.Empty(stats.Maxima);
        Assert.Empty(stats.AppliedReports);
        Assert.Empty(stats.Unlocked);
    }

    [Fact]
    public void Apply_WhenWritingIsInterrupted_ThrowsAndLeavesTheOldFileAndMemoryAsTheyWere()
    {
        var store = FileStore();
        Report(store, Stats((StatKeys.Kills, 1)));
        var before = File.ReadAllBytes(StorePath);
        store.BeforeReplacingFile = _ => throw new IOException("the disk went away");
        var lostReport = Guid.NewGuid();

        Assert.Throws<IOException>(() => Report(store, Stats((StatKeys.Kills, 30)), reportId: lostReport));

        Assert.Equal(before, File.ReadAllBytes(StorePath));
        Assert.Single(Directory.GetFiles(_folder));
        var stats = store.Get(_account);
        Assert.Equal(1, stats.Counters[StatKeys.Kills]);
        Assert.DoesNotContain(lostReport, stats.AppliedReports);
        Assert.False(stats.Unlocked.ContainsKey("hunter"));
        store.BeforeReplacingFile = null;
        Assert.Equal(StatsApplyStatus.Applied, Report(store, Stats((StatKeys.Kills, 30)), reportId: lostReport).Status);
    }

    private static IReadOnlyList<StatsApplyResult> ReportFromEightThreads(IStatsStore store, Guid account, int perThread)
    {
        const int threads = 8;
        var results = new ConcurrentBag<StatsApplyResult>();
        Parallel.For(0, threads, new ParallelOptions { MaxDegreeOfParallelism = threads }, _ =>
        {
            for (var index = 0; index < perThread; index++)
                results.Add(store.Apply(account, Guid.NewGuid(), Stats((StatKeys.Kills, 1), (StatKeys.Games, 2)), Stats((StatKeys.BestScore, index)), Now, Definitions));
        });
        return results.ToList();
    }

    [Fact]
    public void Apply_TwoThousandReportsFromEightThreadsAtOnce_AddUpExactlyAndUnlockEachAchievementOnce()
    {
        var store = MemoryStore();

        var results = ReportFromEightThreads(store, _account, perThread: 250);

        var stats = store.Get(_account);
        Assert.All(results, result => Assert.Equal(StatsApplyStatus.Applied, result.Status));
        Assert.Equal(2000, stats.Counters[StatKeys.Kills]);
        Assert.Equal(4000, stats.Counters[StatKeys.Games]);
        Assert.Equal(249, stats.Maxima[StatKeys.BestScore]);
        Assert.Equal(1, results.Count(result => result.Unlocked.Contains("hunter")));
        Assert.Equal(1, results.Count(result => result.Unlocked.Contains("high-score")));
        Assert.Equal(AccountStats.RememberedReports, stats.AppliedReports.Count);
    }

    [Fact]
    public void Apply_FromEightThreadsAtOnceWithAFile_EndsWithEveryReportSaved()
    {
        var store = FileStore();

        ReportFromEightThreads(store, _account, perThread: 25);

        Assert.Equal(200, store.Get(_account).Counters[StatKeys.Kills]);
        Assert.Equal(200, FileStore().Get(_account).Counters[StatKeys.Kills]);
        Assert.Single(Directory.GetFiles(_folder));
    }
}
