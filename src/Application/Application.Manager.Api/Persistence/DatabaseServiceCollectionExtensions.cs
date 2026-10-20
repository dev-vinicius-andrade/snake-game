using Microsoft.EntityFrameworkCore;

namespace Application.Manager.Api.Persistence;

public static class DatabaseServiceCollectionExtensions
{
    /// <summary>The Postgres connection string. Empty or missing: no database, accounts stay in their JSON files (or in memory).</summary>
    public const string ConnectionKey = "ConnectionStrings:Database";

    /// <summary>Whether the manager brings the database up to the latest migration when it starts. On unless set to false.</summary>
    public const string MigrateOnStartKey = "Database:MigrateOnStart";

    public static bool HasDatabase(this IConfiguration configuration) => !string.IsNullOrWhiteSpace(configuration[ConnectionKey]);

    /// <summary>
    /// The database, when a connection string is configured: the context factory and the start up migration. Nothing connects here; the first
    /// call to a store does. Without a connection string this does nothing.
    /// </summary>
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        if (!configuration.HasDatabase() || services.Any(service => service.ServiceType == typeof(IDbContextFactory<SnakeDbContext>)))
            return services;
        var connection = configuration[ConnectionKey]!.Trim();
        services.AddPooledDbContextFactory<SnakeDbContext>(options => options.UseNpgsql(connection, npgsql => npgsql.MigrationsHistoryTable("migrations_history")));
        services.AddHostedService<AchievementCatalogService>();
        if (!string.Equals(configuration[MigrateOnStartKey], "false", StringComparison.OrdinalIgnoreCase))
            services.AddHostedService<DatabaseMigrationService>();
        return services;
    }
}
