using Application.Manager.Api.Accounts;
using Library.Commons.Game.Domain.Entities;
using Microsoft.Extensions.Time.Testing;

namespace Application.Manager.Api.Tests;

public sealed class AchievementServiceTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 19, 12, 0, 0, TimeSpan.Zero));
    private readonly FileAccountStore _accounts = new(null);
    private readonly FileStatsStore _stats = new(null);
    private readonly Account _account;

    public AchievementServiceTests()
    {
        _account = new Account(Guid.NewGuid(), "Vini", 9291, "pbkdf2-sha256$1$c2FsdA==$aGFzaA==", _time.GetUtcNow(), []);
        _accounts.TryAdd(_account);
    }

    private AchievementService Service(IReadOnlyList<AchievementDefinition>? definitions = null) => new(_accounts, _stats, _time, definitions);

    private static Dictionary<string, long> Stats(params (string Key, long Value)[] values) => values.ToDictionary(value => value.Key, value => value.Value);

    private AccountResult<UnlockedAchievements> Report(AchievementService service, Dictionary<string, long>? counters = null,
        Dictionary<string, long>? maxima = null, Guid? reportId = null, Guid? account = null)
        => service.Report(account ?? _account.Id, reportId ?? Guid.NewGuid(), counters, maxima);

    [Fact]
    public void Definitions_WithoutAnOverride_AreTheDefaults()
    {
        Assert.Same(Achievements.Defaults, Service().Definitions);
    }

    [Fact]
    public void Report_ForAnAccountThatDoesNotExist_FailsWithUnknownAccountAndKeepsNothing()
    {
        var unknown = Guid.NewGuid();

        var result = Report(Service(), Stats((StatKeys.Kills, 1)), account: unknown);

        Assert.Equal(StatsErrors.UnknownAccount, result.Error);
        Assert.Empty(_stats.Get(unknown).Counters);
    }

    [Fact]
    public void Report_UnlocksTheDefaultAchievementsItReaches()
    {
        var result = Report(Service(), Stats((StatKeys.Games, 1), (StatKeys.Kills, 1)));

        Assert.True(result.Succeeded);
        Assert.Equal(new[] { "first-steps", "first-blood" }, result.Value.Ids);
    }

    [Fact]
    public void Report_UsesTheDefinitionsGivenToTheConstructor()
    {
        var service = Service([new AchievementDefinition("eater", "game-icons:sparkles", StatKeys.Foods, 3)]);

        var result = Report(service, Stats((StatKeys.Foods, 3), (StatKeys.Kills, 1)));

        Assert.Equal(new[] { "eater" }, result.Value!.Ids);
    }

    [Fact]
    public void Report_UnlocksAtTheTimeOfTheReport()
    {
        _time.Advance(TimeSpan.FromMinutes(7));

        Report(Service(), Stats((StatKeys.Kills, 1)));

        Assert.Equal(_time.GetUtcNow(), _stats.Get(_account.Id).Unlocked["first-blood"]);
    }

    [Fact]
    public void Report_WithoutAReportId_FailsWithInvalidStats()
    {
        var result = Service().Report(_account.Id, null, Stats((StatKeys.Kills, 1)), null);

        Assert.Equal(StatsErrors.InvalidStats, result.Error);
        Assert.Empty(_stats.Get(_account.Id).Counters);
    }

    [Theory]
    [InlineData(StatKeys.Kills, -1)]
    [InlineData(StatKeys.Kills, StatsReport.MaxCounter + 1)]
    public void Report_WithAValueOutOfRange_FailsWithInvalidStats(string key, long value)
    {
        var result = Report(Service(), Stats((key, value)));

        Assert.Equal(StatsErrors.InvalidStats, result.Error);
    }

    [Fact]
    public void Report_AnUnknownAccountWithABadValue_IsUnknownAccount()
    {
        var result = Report(Service(), Stats((StatKeys.Kills, -1)), account: Guid.NewGuid());

        Assert.Equal(StatsErrors.UnknownAccount, result.Error);
    }

    [Fact]
    public void Report_SentTwice_SucceedsTheSecondTimeWithNothingUnlockedAndCountsOnce()
    {
        var service = Service();
        var reportId = Guid.NewGuid();
        Report(service, Stats((StatKeys.Kills, 1)), reportId: reportId);

        var again = Report(service, Stats((StatKeys.Kills, 1)), reportId: reportId);

        Assert.True(again.Succeeded);
        Assert.Empty(again.Value.Ids);
        Assert.Equal(1, _stats.Get(_account.Id).Counters[StatKeys.Kills]);
    }

    [Fact]
    public void Profile_ForAnAccountThatDoesNotExist_IsNull()
    {
        Assert.Null(Service().Profile(Guid.NewGuid()));
    }

    [Fact]
    public void Profile_WithoutAnyReport_HasEveryStatKeyAtZeroAndNoAchievement()
    {
        var profile = Service().Profile(_account.Id);

        Assert.NotNull(profile);
        Assert.Equal((_account.Id, _account.Display), (profile.Account.Id, profile.Account.Display));
        Assert.Equal(StatKeys.Counters.Concat(StatKeys.Maxima), profile.Stats.Keys);
        Assert.All(profile.Stats.Values, value => Assert.Equal(0, value));
        Assert.Empty(profile.Achievements);
    }

    [Fact]
    public void Profile_ShowsTheCountersAndMaximaReported()
    {
        var service = Service();
        Report(service, Stats((StatKeys.Kills, 3), (StatKeys.PlaySeconds, 90)), Stats((StatKeys.BestScore, 120)));
        Report(service, Stats((StatKeys.Kills, 2)), Stats((StatKeys.BestScore, 80)));

        var stats = service.Profile(_account.Id)!.Stats;

        Assert.Equal(5, stats[StatKeys.Kills]);
        Assert.Equal(90, stats[StatKeys.PlaySeconds]);
        Assert.Equal(120, stats[StatKeys.BestScore]);
        Assert.Equal(0, stats[StatKeys.Deaths]);
        Assert.Equal(StatKeys.Counters.Count + StatKeys.Maxima.Count, stats.Count);
    }

    [Fact]
    public void Profile_ListsTheUnlockedAchievementsByWhenTheyWereUnlocked()
    {
        var service = Service();
        Report(service, Stats((StatKeys.Kills, 1)));
        var first = _time.GetUtcNow();
        _time.Advance(TimeSpan.FromHours(1));
        Report(service, Stats((StatKeys.Games, 25)), Stats((StatKeys.LongestSnake, 60)));
        var second = _time.GetUtcNow();

        var achievements = service.Profile(_account.Id)!.Achievements;

        Assert.Equal(
            new[]
            {
                new UnlockedAchievement("first-blood", first),
                new UnlockedAchievement("first-steps", second),
                new UnlockedAchievement("regular", second),
                new UnlockedAchievement("big-snake", second),
            },
            achievements);
    }

    [Fact]
    public void Profile_KeepsAnAchievementThatIsNoLongerDefined()
    {
        Report(Service([new AchievementDefinition("retired", "game-icons:snake", StatKeys.Kills, 1)]), Stats((StatKeys.Kills, 1)));

        var achievements = Service().Profile(_account.Id)!.Achievements;

        Assert.Equal(new[] { "retired" }, achievements.Select(achievement => achievement.Id));
    }

    [Fact]
    public void Profile_OfAnAccountMadeWithASocialLogin_Loads_LikeAnyOther()
    {
        var accounts = new FileAccountStore(null);
        var throttles = AccountThrottles.Create(_time);
        var service = new AccountService(accounts, throttles, SimpleWordFilter.Default, new RecoveryPhrase(), new SecretHasher(1), _time);
        var social = service.SignInWithExternal("discord", "80351110224678912", "nelly", "Nelly", "203.0.113.7").Value.Account;

        var profile = new AchievementService(accounts, _stats, _time).Profile(social.Id);

        Assert.NotNull(profile);
        Assert.Equal("Nelly", profile!.Account.PublicName);
        Assert.Empty(profile.Achievements);
    }

    private sealed class FakeCatalog(params AchievementEntry[] entries) : IAchievementCatalog
    {
        public List<AchievementEntry> Entries { get; } = entries.ToList();

        public bool IsWritable => true;

        public IReadOnlyList<AchievementEntry> All() => Entries;

        public AchievementChange Save(AchievementEntry entry) => AchievementChange.Saved;

        public AchievementChange Delete(string id) => AchievementChange.Deleted;
    }

    [Fact]
    public void Definitions_FollowTheCatalogAsItChanges_OnlyTheEnabledOnes()
    {
        var catalog = new FakeCatalog(new AchievementEntry("a-one", "game-icons:snake", StatKeys.Games, 1), new AchievementEntry("b-two", "game-icons:snake", StatKeys.Games, 5, Enabled: false));
        var service = new AchievementService(_accounts, _stats, _time, catalog: catalog);

        Assert.Equal(new[] { "a-one" }, service.Definitions.Select(definition => definition.Id));

        catalog.Entries.Add(new AchievementEntry("c-three", "game-icons:snake", StatKeys.Kills, 3));

        Assert.Equal(new[] { "a-one", "c-three" }, service.Definitions.Select(definition => definition.Id));
    }

    [Fact]
    public void ACustomAchievement_UnlocksLikeAnyOther()
    {
        var catalog = new FakeCatalog(new AchievementEntry("snacker", "game-icons:sparkles", StatKeys.Foods, 10));
        var service = new AchievementService(_accounts, _stats, _time, catalog: catalog);
        var account = _account;

        var result = service.Report(account.Id, Guid.NewGuid(), new Dictionary<string, long> { [StatKeys.Foods] = 12 }, null);

        Assert.Equal(new[] { "snacker" }, result.Value.Ids);
    }
}
