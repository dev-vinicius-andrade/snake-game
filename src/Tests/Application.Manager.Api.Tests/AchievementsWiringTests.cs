using Application.Manager.Api.Accounts;
using Library.Commons.Game.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Manager.Api.Tests;

public sealed class AchievementsWiringTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "snake-stats-wiring-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private static ServiceProvider Provider(string? statsPath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [AccountsServiceCollectionExtensions.StatsPathKey] = statsPath })
            .Build();
        return new ServiceCollection().AddLogging().AddAccounts(configuration).BuildServiceProvider();
    }

    private static Guid Register(ServiceProvider provider)
        => provider.GetRequiredService<AccountService>().Register("Vini", null, null, "203.0.113.7").Value!.Account.Id;

    [Fact]
    public void AddAccounts_ResolvesOneAchievementServiceThatSeesTheRegisteredAccounts()
    {
        using var provider = Provider(string.Empty);
        var account = Register(provider);

        var service = provider.GetRequiredService<AchievementService>();
        var result = service.Report(account, Guid.NewGuid(), new Dictionary<string, long> { [StatKeys.Kills] = 1 }, null);

        Assert.Same(service, provider.GetRequiredService<AchievementService>());
        Assert.IsType<FileStatsStore>(provider.GetRequiredService<IStatsStore>());
        Assert.Same(Achievements.Defaults, service.Definitions);
        Assert.Equal(new[] { "first-blood" }, result.Value!.Ids);
    }

    [Fact]
    public void AddAccounts_WithAStatsPath_KeepsTheStatsInThatFile()
    {
        var path = Path.Combine(_folder, "stats.json");
        var account = Guid.NewGuid();
        using (var provider = Provider(path))
            provider.GetRequiredService<IStatsStore>().Apply(account, Guid.NewGuid(), new Dictionary<string, long> { [StatKeys.Kills] = 4 }, null,
                DateTimeOffset.UtcNow, Achievements.Defaults);

        using var restarted = Provider(path);

        Assert.True(File.Exists(path));
        Assert.Equal(4, restarted.GetRequiredService<IStatsStore>().Get(account).Counters[StatKeys.Kills]);
    }
}
