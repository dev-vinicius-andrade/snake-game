using Microsoft.EntityFrameworkCore;

namespace Application.Manager.Api.Persistence;

/// <summary>
/// Brings the database up to the latest migration before the manager serves anything (hosted services start before the server listens).
/// When it cannot, the manager does not start: running on a database of another shape is worse than not running. Switch it off with
/// <c>Database:MigrateOnStart=false</c> to run the migrations by hand (<c>dotnet ef database update</c>) or from a pipeline.
/// </summary>
public sealed class DatabaseMigrationService(IDbContextFactory<SnakeDbContext> factory, ILogger<DatabaseMigrationService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var context = await factory.CreateDbContextAsync(cancellationToken);
        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("The database is up to date");
            return;
        }
        logger.LogInformation("Applying {Count} database migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
        await context.Database.MigrateAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
