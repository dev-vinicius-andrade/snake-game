using Application.Manager.Api.Accounts;
using Application.Manager.Api.Persistence;
using Library.Commons.Game.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Manager.Api.Tests.Persistence;

public sealed class DatabaseImporterTests : IDisposable
{
    private static readonly DateTimeOffset Created = new DateTimeOffset(2026, 10, 19, 12, 34, 56, TimeSpan.FromMinutes(330)).AddTicks(1_234_560);

    private readonly PersistenceTestHost _host = new();
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "snake-import-" + Guid.NewGuid().ToString("N"));

    public Guid Ana { get; } = Guid.NewGuid();

    public Guid Bob { get; } = Guid.NewGuid();

    public DatabaseImporterTests() => Directory.CreateDirectory(_folder);

    public void Dispose()
    {
        _host.Dispose();
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private string AccountsPath => Path.Combine(_folder, "accounts.json");

    private string StatsPath => Path.Combine(_folder, "stats.json");

    private static readonly IReadOnlyList<AchievementDefinition> Definitions =
    [
        new("first-blood", "game-icons:pointy-sword", StatKeys.Kills, 1),
        new("hunter", "game-icons:crosshair", StatKeys.Kills, 25),
    ];

    private void WriteFiles()
    {
        var accounts = new FileAccountStore(AccountsPath);
        accounts.TryAdd(new Account(Ana, "Ana", 1, "pbkdf2$hash", Created, [new DeviceToken("a-token", Created.AddMinutes(1), "laptop")], "Anita"));
        accounts.TryAdd(new Account(Bob, "octocat", 2, Account.NoPhrase, Created, [new DeviceToken("b-token", Created)], null, [new ExternalLogin("github", "abc123", Created)]));
        var stats = new FileStatsStore(StatsPath);
        stats.Apply(Ana, Guid.NewGuid(), new Dictionary<string, long> { [StatKeys.Kills] = 30 }, new Dictionary<string, long> { [StatKeys.BestScore] = 250 }, Created, Definitions);
    }

    private ImportResult Import(string? accountsPath = null, string? statsPath = null)
        => DatabaseImporter.Import(_host.Factory, Definitions, accountsPath ?? AccountsPath, statsPath ?? StatsPath);

    [Fact]
    public void Import_TheAccountsOfTheFile_KeepTheirIdsNamesDevicesAndSocialLogins()
    {
        WriteFiles();

        var result = Import();

        Assert.Equal(2, result.Accounts);
        var store = _host.Accounts();
        var ana = store.Find(Ana)!;
        Assert.Equal(("Ana", 1, "pbkdf2$hash", "Anita"), (ana.Username, ana.Tag, ana.PhraseHash, ana.DisplayName));
        Assert.Equal("a-token", Assert.Single(ana.Devices).Hash);
        Assert.Equal(Created.UtcDateTime, ana.CreatedAt.UtcDateTime);
        Assert.Equal(Bob, store.FindByExternalLogin("github", "abc123")!.Id);
    }

    [Fact]
    public void Import_TheStatsOfTheFile_KeepCountersMaximaReportsAndUnlocks()
    {
        WriteFiles();

        var result = Import();

        Assert.Equal(1, result.Stats);
        var stats = _host.Stats().Get(Ana);
        Assert.Equal(30, stats.Counters[StatKeys.Kills]);
        Assert.Equal(250, stats.Maxima[StatKeys.BestScore]);
        Assert.Single(stats.AppliedReports);
        Assert.Equal(new[] { "first-blood", "hunter" }, stats.Unlocked.Keys.Order());
        Assert.Equal(Created.UtcDateTime, stats.Unlocked["hunter"].UtcDateTime);
    }

    [Fact]
    public void Import_ARepeatedReportIsStillRecognisedAfterwards()
    {
        WriteFiles();
        var reportId = new FileStatsStore(StatsPath).Get(Ana).AppliedReports.Single();
        Import();

        var again = _host.Stats().Apply(Ana, reportId, new Dictionary<string, long> { [StatKeys.Kills] = 5 }, new Dictionary<string, long>(), Created, Definitions);

        Assert.Equal(StatsApplyStatus.AlreadyApplied, again.Status);
    }

    [Fact]
    public void Import_TheCatalogHasTheAchievementsTheStatsUnlocked()
    {
        WriteFiles();

        Import();

        using var context = _host.Factory.CreateDbContext();
        Assert.Equal(new[] { "first-blood", "hunter" }, context.Achievements.Select(row => row.Id).OrderBy(id => id));
    }

    [Fact]
    public void Import_RunAgain_ImportsNothingMore()
    {
        WriteFiles();
        Import();

        var second = Import();

        Assert.Equal(new ImportResult(0, 0, Skipped: true), second);
        Assert.Equal(2, _host.Factory.CreateDbContext().Accounts.Count());
    }

    [Fact]
    public void Import_ADatabaseThatAlreadyHasAccounts_IsLeftAlone()
    {
        _host.Accounts().TryAdd(new Account(Guid.NewGuid(), "Zed", 9, Account.NoPhrase, Created, [new DeviceToken("z", Created)]));
        WriteFiles();

        var result = Import();

        Assert.True(result.Skipped);
        Assert.Null(_host.Accounts().Find(Ana));
    }

    [Fact]
    public void Import_FilesThatAreNotThere_OrNoPaths_ImportNothing()
    {
        Assert.Equal(new ImportResult(0, 0, Skipped: false), Import(Path.Combine(_folder, "none.json"), Path.Combine(_folder, "none-stats.json")));
        Assert.Equal(new ImportResult(0, 0, Skipped: false), DatabaseImporter.Import(_host.Factory, Definitions, null, ""));
    }

    [Fact]
    public void Import_AnUnlockOfAnAchievementThatNoLongerExists_IsKeptOutAndTheRestIsImported()
    {
        WriteFiles();

        var result = DatabaseImporter.Import(_host.Factory, [Definitions[0]], AccountsPath, StatsPath);

        Assert.Equal(1, result.Stats);
        Assert.Equal(new[] { "first-blood" }, _host.Stats().Get(Ana).Unlocked.Keys);
        Assert.Equal(30, _host.Stats().Get(Ana).Counters[StatKeys.Kills]);
    }
}
