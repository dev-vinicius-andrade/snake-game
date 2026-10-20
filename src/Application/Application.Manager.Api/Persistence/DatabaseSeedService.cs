using Application.Manager.Api.Accounts;
using Application.Manager.Api.Gameplay;
using Microsoft.EntityFrameworkCore;

namespace Application.Manager.Api.Persistence;

/// <summary>
/// Fills the database with what the game starts from, when the manager starts (after the migrations): the achievements of the game, the
/// gameplay config (the old gameplay file when there is one, else the defaults), and, into an empty database, the accounts and stats of the old
/// JSON files. Each step runs again safely and none stops the manager: a failure is logged, and the lazy paths (a stats store adds the achievement
/// it unlocks, the gameplay store seeds itself on first use) still work.
/// </summary>
public sealed class DatabaseSeedService(IDbContextFactory<SnakeDbContext> factory, AchievementService achievements, IConfiguration configuration,
    IServiceProvider services, ILogger<DatabaseSeedService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        Step("achievements", () => AchievementCatalog.Sync(factory, achievements.Definitions));
        Step("gameplay config", () => _ = services.GetService<IGameplayConfigStore>()?.Current);
        Step("files", () => DatabaseImporter.Import(factory, achievements.Definitions, configuration[AccountsServiceCollectionExtensions.StorePathKey],
            configuration[AccountsServiceCollectionExtensions.StatsPathKey], logger));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void Step(string what, Action run)
    {
        try
        {
            run();
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Putting the {Step} in the database failed; the manager goes on without it", what);
        }
    }
}
