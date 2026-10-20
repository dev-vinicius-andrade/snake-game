using Application.Manager.Api.Accounts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Manager.Api.Tests;

public sealed class AccountsWiringTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "snake-accounts-wiring-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private static ServiceProvider Provider(string? storePath)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [AccountsServiceCollectionExtensions.StorePathKey] = storePath })
            .Build();
        return new ServiceCollection().AddLogging().AddAccounts(configuration).BuildServiceProvider();
    }

    [Fact]
    public void AddAccounts_ResolvesOneAccountServiceWithEverythingItNeeds()
    {
        using var provider = Provider(string.Empty);

        var service = provider.GetRequiredService<AccountService>();

        Assert.Same(service, provider.GetRequiredService<AccountService>());
        Assert.IsType<FileAccountStore>(provider.GetRequiredService<IAccountStore>());
        Assert.Same(SimpleWordFilter.Default, provider.GetRequiredService<IWordFilter>());
        Assert.True(service.Availability("Vini", null, "203.0.113.7").Value!.Available);
    }

    [Fact]
    public void AddAccounts_WithAStorePath_KeepsTheAccountsInThatFile()
    {
        var path = Path.Combine(_folder, "accounts.json");
        Guid id;
        using (var provider = Provider(path))
            id = provider.GetRequiredService<AccountService>().Register("Vini", null, null, "203.0.113.7").Value!.Account.Id;

        using var restarted = Provider(path);

        Assert.True(File.Exists(path));
        Assert.NotNull(restarted.GetRequiredService<IAccountStore>().Find(id));
    }

    [Fact]
    public void AddAccounts_ReadsTheSocialLoginKeysFromTheOAuthSection_AndOffersOnlyThoseThatAreComplete()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["OAuth:PublicApiUrl"] = "https://snake.example.com/api",
                ["OAuth:Providers:github:ClientId"] = "id",
                ["OAuth:Providers:github:ClientSecret"] = "secret",
                ["OAuth:Providers:discord:ClientId"] = "id-only",
            })
            .Build();
        using var provider = new ServiceCollection().AddLogging().AddAccounts(configuration).BuildServiceProvider();

        var providers = provider.GetRequiredService<Application.Manager.Api.Accounts.OAuth.OAuthService>().Providers();

        Assert.Equal(new[] { "github" }, providers.Select(entry => entry.Id));
    }

    [Fact]
    public void AddAccounts_WithNoOAuthSection_OffersNoSocialLogin()
    {
        using var provider = Provider(string.Empty);

        Assert.Empty(provider.GetRequiredService<Application.Manager.Api.Accounts.OAuth.OAuthService>().Providers());
    }
}
