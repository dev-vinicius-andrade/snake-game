using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Application.Manager.Api.Accounts;

public static class AccountsServiceCollectionExtensions
{
    /// <summary>The JSON file accounts are kept in. Empty or missing: memory only, gone at restart (like the gameplay config store).</summary>
    public const string StorePathKey = "AccountsConfiguration:StorePath";

    /// <summary>Everything <see cref="AccountService"/> needs, as singletons.</summary>
    public static IServiceCollection AddAccounts(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IAccountStore>(provider =>
            new FileAccountStore(configuration[StorePathKey], provider.GetService<ILoggerFactory>()?.CreateLogger<FileAccountStore>()));
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
            provider.GetService<ILogger<AccountService>>()));
        return services;
    }
}
