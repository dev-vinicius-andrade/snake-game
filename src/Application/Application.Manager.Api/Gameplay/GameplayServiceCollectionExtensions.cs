using Application.Manager.Api.Persistence;
using Domain.Game.Rules;
using Microsoft.EntityFrameworkCore;

namespace Application.Manager.Api.Gameplay;

public static class GameplayServiceCollectionExtensions
{
    /// <summary>The file the gameplay config lives in without a database, and is read from once to fill an empty database.</summary>
    public const string StorePathKey = "GameplayConfiguration:StorePath";

    /// <summary>
    /// The gameplay config store: the database tables when a connection string is configured (an empty database is filled from the old gameplay file
    /// when it exists, else the defaults), else the file (or memory only, with no path).
    /// </summary>
    public static IServiceCollection AddGameplayConfig(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddDatabase(configuration);
        services.AddSingleton<IGameplayConfigStore>(provider => configuration.HasDatabase()
            ? new EfGameplayConfigStore(
                provider.GetRequiredService<IDbContextFactory<SnakeDbContext>>(),
                provider.GetService<TimeProvider>() ?? TimeProvider.System,
                () => SeedFromFile(configuration[StorePathKey], provider.GetService<ILoggerFactory>()),
                provider.GetService<ILoggerFactory>()?.CreateLogger<EfGameplayConfigStore>())
            : new GameplayConfigStore(configuration[StorePathKey], provider.GetService<ILoggerFactory>()?.CreateLogger<GameplayConfigStore>()));
        return services;
    }

    private static GameplayConfig SeedFromFile(string? path, ILoggerFactory? loggers)
        => string.IsNullOrWhiteSpace(path) || !File.Exists(path)
            ? GameplayConfig.Defaults
            : new GameplayConfigStore(path, loggers?.CreateLogger<GameplayConfigStore>()).Current.Config;
}
