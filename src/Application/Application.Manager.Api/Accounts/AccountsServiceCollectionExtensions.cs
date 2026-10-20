using Microsoft.Extensions.DependencyInjection.Extensions;

using Application.Manager.Api.Accounts.OAuth;
using Application.Manager.Api.Persistence;

namespace Application.Manager.Api.Accounts;

public static class AccountsServiceCollectionExtensions
{
    /// <summary>The JSON file accounts are kept in. Empty or missing: memory only, gone at restart (like the gameplay config store).</summary>
    public const string StorePathKey = "AccountsConfiguration:StorePath";

    /// <summary>The secret that keys the hash of social account ids. Keep it: changing it makes every social account look new.</summary>
    public const string LoginPepperKey = "AccountsConfiguration:LoginPepper";

    /// <summary>The JSON file stats and unlocked achievements are kept in. Empty or missing: memory only, gone at restart.</summary>
    public const string StatsPathKey = "AccountsConfiguration:StatsPath";

    /// <summary>Everything <see cref="AccountService"/> and <see cref="AchievementService"/> need, as singletons.</summary>
    public static IServiceCollection AddAccounts(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.TryAddSingleton(TimeProvider.System);
        services.AddDatabase(configuration);
        if (configuration.HasDatabase())
            services.AddSingleton<IAccountStore, EfAccountStore>();
        else
            services.AddSingleton<IAccountStore>(provider =>
                new FileAccountStore(configuration[StorePathKey], provider.GetService<ILoggerFactory>()?.CreateLogger<FileAccountStore>()));
        if (configuration.HasDatabase())
            services.AddSingleton<IRoleStore, EfRoleStore>();
        else
            services.AddSingleton<IRoleStore, MemoryRoleStore>();
        services.AddSingleton<IWordFilter>(SimpleWordFilter.Default);
        services.AddSingleton(_ => new RecoveryPhrase());
        services.AddSingleton(_ => new SecretHasher());
        services.AddSingleton(provider => AccountThrottles.Create(provider.GetRequiredService<TimeProvider>()));
        services.AddSingleton(provider => new AccountService(
            provider.GetRequiredService<IAccountStore>(),
            provider.GetRequiredService<AccountThrottles>(),
            provider.GetRequiredService<IWordFilter>(),
            provider.GetRequiredService<RecoveryPhrase>(),
            provider.GetRequiredService<SecretHasher>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetService<ILogger<AccountService>>(),
            loginPepper: configuration[LoginPepperKey]));
        services.AddSingleton(configuration.GetSection(OAuthOptions.SectionName).Get<OAuthOptions>() ?? new OAuthOptions());
        services.AddHttpClient<IOAuthClient, HttpOAuthClient>(client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton(provider => new OAuthService(
            provider.GetRequiredService<OAuthOptions>(),
            provider.GetRequiredService<IOAuthClient>(),
            provider.GetRequiredService<AccountService>(),
            provider.GetRequiredService<TimeProvider>(),
            provider.GetService<ILogger<OAuthService>>()));
        if (configuration.HasDatabase())
            services.AddSingleton<IStatsStore, EfStatsStore>();
        else
            services.AddSingleton<IStatsStore>(provider =>
                new FileStatsStore(configuration[StatsPathKey], provider.GetService<ILoggerFactory>()?.CreateLogger<FileStatsStore>()));
        services.AddSingleton(provider => new AchievementService(
            provider.GetRequiredService<IAccountStore>(),
            provider.GetRequiredService<IStatsStore>(),
            provider.GetRequiredService<TimeProvider>(),
            logger: provider.GetService<ILogger<AchievementService>>()));
        return services;
    }
}
