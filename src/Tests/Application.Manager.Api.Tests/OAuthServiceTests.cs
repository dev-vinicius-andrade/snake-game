using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Web;
using Application.Manager.Api.Accounts;
using Application.Manager.Api.Accounts.OAuth;
using Microsoft.Extensions.Time.Testing;

namespace Application.Manager.Api.Tests;

public sealed class OAuthServiceTests
{
    private const string Ip = "203.0.113.7";
    private const string Api = "https://snake.example.com/api";

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly FakeClient _client = new();
    private readonly AccountService _accounts;
    private readonly OAuthOptions _options = new()
    {
        PublicApiUrl = Api + "/",
        Providers = { ["github"] = new OAuthCredentials { ClientId = "gh-id", ClientSecret = "gh-secret" }, ["discord"] = new OAuthCredentials { ClientId = "dc-id", ClientSecret = "dc-secret" } },
    };

    private sealed class FakeClient : IOAuthClient
    {
        public string? AccessToken { get; set; } = "provider-token";
        public string UserJson { get; set; } = """{ "id": 583231, "login": "octocat", "name": "The Octocat" }""";
        public List<(string Provider, string Code, string RedirectUri, string Verifier, string ClientId)> Exchanges { get; } = [];
        public List<string> UserLookups { get; } = [];
        public List<string> TokenUrls { get; } = [];
        public List<string> UserUrls { get; } = [];
        public bool Throws { get; set; }

        public Task<string?> ExchangeCodeAsync(OAuthProviderInfo provider, OAuthCredentials credentials, string code, string redirectUri, string codeVerifier, CancellationToken cancellationToken)
        {
            if (Throws) throw new HttpRequestException("provider down");
            Exchanges.Add((provider.Id, code, redirectUri, codeVerifier, credentials.ClientId));
            TokenUrls.Add(provider.TokenUrl);
            return Task.FromResult(AccessToken);
        }

        public Task<JsonElement?> GetUserAsync(OAuthProviderInfo provider, string accessToken, CancellationToken cancellationToken)
        {
            UserLookups.Add(accessToken);
            UserUrls.Add(provider.UserUrl);
            return Task.FromResult<JsonElement?>(JsonDocument.Parse(UserJson).RootElement);
        }
    }

    public OAuthServiceTests() => _accounts = new AccountService(new FileAccountStore(null), AccountThrottles.Create(_time), null, new RecoveryPhrase(), new SecretHasher(1), _time);

    private OAuthService Service(OAuthOptions? options = null) => new(options ?? _options, _client, _accounts, _time);

    private static Dictionary<string, string?> Query(string url)
        => HttpUtility.ParseQueryString(new Uri(url).Query).AllKeys.ToDictionary(key => key!, key => HttpUtility.ParseQueryString(new Uri(url).Query)[key]);

    private static string Challenge(string verifier) => Convert.ToBase64String(SHA256.HashData(Encoding.ASCII.GetBytes(verifier))).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    [Fact]
    public void Providers_ListsOnlyThoseWithACredential_InTheOrderTheGameKnowsThem()
    {
        Assert.Equal(new[] { "github", "discord" }, Service().Providers().Select(provider => provider.Id));
        Assert.Equal("GitHub", Service().Providers()[0].Title);
    }

    [Fact]
    public void Providers_IsEmptyWithoutAPublicAddress_BecauseNoRedirectAddressCanBeMade()
    {
        _options.PublicApiUrl = "";

        Assert.Empty(Service().Providers());
    }

    [Fact]
    public void Providers_ACredentialWithNoSecretIsNotEnough()
    {
        _options.Providers["github"].ClientSecret = " ";

        Assert.Equal(new[] { "discord" }, Service().Providers().Select(provider => provider.Id));
    }

    [Fact]
    public void Start_SendsThePlayerToTheProviderWithOnlyWhatItNeeds_AndAStateToCheckLater()
    {
        var started = Service().Start("discord");

        Assert.True(started.Succeeded, started.Error);
        var url = started.Value.Url;
        Assert.StartsWith("https://discord.com/oauth2/authorize?", url);
        var query = Query(url);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("dc-id", query["client_id"]);
        Assert.Equal(Api + "/accounts/oauth/discord/callback", query["redirect_uri"]);
        Assert.Equal("identify", query["scope"]);
        Assert.Equal("S256", query["code_challenge_method"]);
        Assert.False(string.IsNullOrEmpty(query["code_challenge"]));
        Assert.Equal(started.Value.State, query["state"]);
        Assert.DoesNotContain("secret", url, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Start_AProviderThatAsksForNoScopeSendsNone()
    {
        Assert.DoesNotContain("scope=", Service().Start("github").Value.Url);
    }

    [Theory]
    [InlineData("myspace")]
    [InlineData("google")]
    [InlineData("")]
    [InlineData(null)]
    public void Start_AnUnknownOrUnconfiguredProviderIsRefused(string? provider)
        => Assert.Equal(AccountErrors.UnknownProvider, Service().Start(provider).Error);

    [Fact]
    public async Task Complete_ExchangesTheCodeWithTheProofOfTheStart_AndGivesATicketThatSignsIn()
    {
        var service = Service();
        var started = service.Start("github").Value;

        var completed = await service.CompleteAsync("github", "the-code", started.State, started.State, Ip, CancellationToken.None);

        Assert.True(completed.Succeeded, completed.Error);
        var exchange = Assert.Single(_client.Exchanges);
        Assert.Equal(("github", "the-code", Api + "/accounts/oauth/github/callback", "gh-id"), (exchange.Provider, exchange.Code, exchange.RedirectUri, exchange.ClientId));
        Assert.Equal(Query(started.Url)["code_challenge"], Challenge(exchange.Verifier));
        Assert.Equal(new[] { "provider-token" }, _client.UserLookups);

        var redeemed = service.Redeem(completed.Value);
        Assert.True(redeemed.Succeeded, redeemed.Error);
        Assert.Equal("octocat", redeemed.Value.Account.Username);
        Assert.Equal("The Octocat", redeemed.Value.Account.PublicName);
        Assert.Equal(redeemed.Value.Account.Id, _accounts.Authenticate(redeemed.Value.Token)?.Id);
    }

    [Fact]
    public async Task Redeem_AnswersOnce_SoALeakedAddressIsOfNoUse()
    {
        var service = Service();
        var started = service.Start("github").Value;
        var ticket = (await service.CompleteAsync("github", "c", started.State, started.State, Ip, CancellationToken.None)).Value;

        Assert.True(service.Redeem(ticket).Succeeded);
        Assert.Equal(AccountErrors.InvalidTicket, service.Redeem(ticket).Error);
    }

    [Fact]
    public async Task Redeem_ATicketExpiresQuickly()
    {
        var service = Service();
        var started = service.Start("github").Value;
        var ticket = (await service.CompleteAsync("github", "c", started.State, started.State, Ip, CancellationToken.None)).Value;

        _time.Advance(TimeSpan.FromMinutes(3));

        Assert.Equal(AccountErrors.InvalidTicket, service.Redeem(ticket).Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("nope")]
    public void Redeem_AWrongTicketIsRefused(string ticket)
        => Assert.Equal(AccountErrors.InvalidTicket, Service().Redeem(ticket).Error);

    [Fact]
    public async Task Complete_AStateThatIsNotTheOneInTheBrowserIsRefused_BeforeAnythingIsAskedOfTheProvider()
    {
        var service = Service();
        var started = service.Start("github").Value;

        var completed = await service.CompleteAsync("github", "code", started.State, "another-browsers-state", Ip, CancellationToken.None);

        Assert.Equal(AccountErrors.InvalidState, completed.Error);
        Assert.Empty(_client.Exchanges);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public async Task Complete_WithoutTheBrowsersState_IsRefused(string? cookie)
    {
        var service = Service();
        var started = service.Start("github").Value;

        var completed = await service.CompleteAsync("github", "code", started.State, cookie, Ip, CancellationToken.None);

        Assert.Equal(AccountErrors.InvalidState, completed.Error);
    }

    [Fact]
    public async Task Complete_AStateWorksOnce()
    {
        var service = Service();
        var started = service.Start("github").Value;
        await service.CompleteAsync("github", "code", started.State, started.State, Ip, CancellationToken.None);

        var again = await service.CompleteAsync("github", "code", started.State, started.State, Ip, CancellationToken.None);

        Assert.Equal(AccountErrors.InvalidState, again.Error);
    }

    [Fact]
    public async Task Complete_AStateMadeForAnotherProviderIsRefused()
    {
        var service = Service();
        var started = service.Start("github").Value;

        var completed = await service.CompleteAsync("discord", "code", started.State, started.State, Ip, CancellationToken.None);

        Assert.Equal(AccountErrors.InvalidState, completed.Error);
    }

    [Fact]
    public async Task Complete_AStateExpires()
    {
        var service = Service();
        var started = service.Start("github").Value;
        _time.Advance(TimeSpan.FromMinutes(11));

        var completed = await service.CompleteAsync("github", "code", started.State, started.State, Ip, CancellationToken.None);

        Assert.Equal(AccountErrors.InvalidState, completed.Error);
    }

    [Fact]
    public async Task Complete_ARefusalFromTheProviderIsAFailureNotACrash()
    {
        var service = Service();
        var started = service.Start("github").Value;
        _client.AccessToken = null;

        var completed = await service.CompleteAsync("github", "bad", started.State, started.State, Ip, CancellationToken.None);

        Assert.Equal(AccountErrors.ProviderFailed, completed.Error);
    }

    [Fact]
    public async Task Complete_AProviderThatIsDownIsAFailureNotACrash()
    {
        var service = Service();
        var started = service.Start("github").Value;
        _client.Throws = true;

        var completed = await service.CompleteAsync("github", "code", started.State, started.State, Ip, CancellationToken.None);

        Assert.Equal(AccountErrors.ProviderFailed, completed.Error);
    }

    [Fact]
    public async Task Complete_AProfileWithoutAnIdIsAFailure()
    {
        var service = Service();
        var started = service.Start("github").Value;
        _client.UserJson = """{ "login": "octocat" }""";

        var completed = await service.CompleteAsync("github", "code", started.State, started.State, Ip, CancellationToken.None);

        Assert.Equal(AccountErrors.ProviderFailed, completed.Error);
    }

    [Fact]
    public async Task Complete_ABlankCodeIsRefusedWithoutAskingTheProvider()
    {
        var service = Service();
        var started = service.Start("github").Value;

        var completed = await service.CompleteAsync("github", " ", started.State, started.State, Ip, CancellationToken.None);

        Assert.Equal(AccountErrors.ProviderFailed, completed.Error);
        Assert.Empty(_client.Exchanges);
    }

    [Fact]
    public async Task Complete_ReturningPlayersGetTheirAccountBack()
    {
        var service = Service();
        var first = service.Start("github").Value;
        var second = service.Start("github").Value;

        var one = service.Redeem((await service.CompleteAsync("github", "a", first.State, first.State, Ip, CancellationToken.None)).Value).Value;
        var two = service.Redeem((await service.CompleteAsync("github", "b", second.State, second.State, Ip, CancellationToken.None)).Value).Value;

        Assert.Equal(one.Account.Id, two.Account.Id);
    }

    [Fact]
    public async Task AProviderCanBePointedAtOtherAddresses_ForATestServerOrGitHubEnterprise()
    {
        _options.Providers["github"].AuthorizeUrl = "http://localhost:9999/authorize";
        _options.Providers["github"].TokenUrl = "http://localhost:9999/token";
        _options.Providers["github"].UserUrl = "http://localhost:9999/user";
        var service = Service();

        var started = service.Start("github").Value;
        await service.CompleteAsync("github", "code", started.State, started.State, Ip, CancellationToken.None);

        Assert.StartsWith("http://localhost:9999/authorize?", started.Url);
        Assert.Equal("http://localhost:9999/token", _client.TokenUrls.Single());
        Assert.Equal("http://localhost:9999/user", _client.UserUrls.Single());
    }
}
