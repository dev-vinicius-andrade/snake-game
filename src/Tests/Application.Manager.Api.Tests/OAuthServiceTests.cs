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
        public int OpenIdChecks { get; private set; }
        public bool OpenIdValid { get; set; } = true;
        public string? SteamPersona { get; set; } = "Gaben";
        public string? PersonaFor { get; private set; }
        public string? PersonaKey { get; private set; }
        public bool PersonaAsked => PersonaFor is not null;

        public Task<bool> VerifyOpenIdAsync(OAuthProviderInfo provider, IReadOnlyDictionary<string, string> answer, CancellationToken cancellationToken)
        {
            OpenIdChecks++;
            return Task.FromResult(OpenIdValid);
        }

        public Task<string?> GetSteamPersonaAsync(OAuthProviderInfo provider, string apiKey, string steamId, CancellationToken cancellationToken)
        {
            PersonaFor = steamId;
            PersonaKey = apiKey;
            return Task.FromResult(SteamPersona);
        }

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
        Assert.Equal("The Octocat", redeemed.Value.Account.Username);
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

    private const string SteamId = "76561198000000001";

    private void AllowSteam() => _options.Providers["steam"] = new OAuthCredentials { ClientSecret = "web-api-key" };

    private Dictionary<string, string> SteamReturn(string state, Action<Dictionary<string, string>>? change = null)
    {
        var query = new Dictionary<string, string>
        {
            ["state"] = state,
            ["openid.ns"] = "http://specs.openid.net/auth/2.0",
            ["openid.mode"] = "id_res",
            ["openid.op_endpoint"] = "https://steamcommunity.com/openid/login",
            ["openid.claimed_id"] = $"https://steamcommunity.com/openid/id/{SteamId}",
            ["openid.identity"] = $"https://steamcommunity.com/openid/id/{SteamId}",
            ["openid.return_to"] = $"{Api}/accounts/oauth/steam/callback?state={state}",
            ["openid.response_nonce"] = "2026-10-20T12:00:00Zabc",
            ["openid.assoc_handle"] = "1234567890",
            ["openid.signed"] = "signed,op_endpoint,claimed_id,identity,return_to,response_nonce,assoc_handle",
            ["openid.sig"] = "signature",
        };
        change?.Invoke(query);
        return query;
    }

    [Fact]
    public void Steam_IsOfferedWithOnlyTheWebApiKey_NoClientId()
    {
        AllowSteam();

        Assert.Contains("steam", Service().Providers().Select(provider => provider.Id));
        Assert.Equal("Steam", Service().Providers().Single(provider => provider.Id == "steam").Title);
    }

    [Fact]
    public void Steam_WithoutTheKeyIsNotOffered()
    {
        _options.Providers["steam"] = new OAuthCredentials { ClientId = "something" };

        Assert.DoesNotContain("steam", Service().Providers().Select(provider => provider.Id));
    }

    [Fact]
    public void Steam_StartSendsTheBrowserToSteam_WithTheStateInTheReturnAddress()
    {
        AllowSteam();

        var started = Service().Start("steam").Value;

        Assert.StartsWith("https://steamcommunity.com/openid/login?", started.Url);
        var query = Query(started.Url);
        Assert.Equal($"{Api}/accounts/oauth/steam/callback?state={started.State}", query["openid.return_to"]);
        Assert.Equal("https://snake.example.com/", query["openid.realm"]);
        Assert.DoesNotContain("web-api-key", started.Url);
    }

    [Fact]
    public async Task Steam_CompleteChecksTheAnswerWithSteam_AsksForTheNameAndGivesATicket()
    {
        AllowSteam();
        _client.SteamPersona = "Gaben";
        var service = Service();
        var started = service.Start("steam").Value;

        var completed = await service.CompleteOpenIdAsync("steam", SteamReturn(started.State), started.State, Ip, CancellationToken.None);

        Assert.True(completed.Succeeded, completed.Error);
        Assert.Equal(1, _client.OpenIdChecks);
        Assert.Equal((SteamId, "web-api-key"), (_client.PersonaFor, _client.PersonaKey));
        var signedIn = service.Redeem(completed.Value).Value;
        Assert.Equal("Gaben", signedIn.Account.PublicName);
        Assert.Equal(LoginIdentifier.Hash("steam", SteamId, AccountService.DevelopmentPepper), Assert.Single(signedIn.Account.Logins).IdHash);
    }

    [Fact]
    public async Task Steam_APlayerWithNoUsableNameIsStillSignedIn_AsPlayer()
    {
        AllowSteam();
        _client.SteamPersona = null;
        var service = Service();
        var started = service.Start("steam").Value;

        var completed = await service.CompleteOpenIdAsync("steam", SteamReturn(started.State), started.State, Ip, CancellationToken.None);

        Assert.Equal("Player", service.Redeem(completed.Value).Value.Account.PublicName);
    }

    [Theory]
    [InlineData("mode")]
    [InlineData("endpoint")]
    [InlineData("returnTo")]
    [InlineData("claimed")]
    [InlineData("identity")]
    [InlineData("verify")]
    public async Task Steam_AnythingOddInTheAnswerIsAFailure_BeforeAPlayerIsSignedIn(string what)
    {
        AllowSteam();
        var service = Service();
        var started = service.Start("steam").Value;
        var answer = SteamReturn(started.State, query =>
        {
            switch (what)
            {
                case "mode": query["openid.mode"] = "setup_needed"; break;
                case "endpoint": query["openid.op_endpoint"] = "https://evil.example.com/openid/login"; break;
                case "returnTo": query["openid.return_to"] = "https://evil.example.com/cb?state=" + started.State; break;
                case "claimed": query["openid.claimed_id"] = "https://evil.example.com/openid/id/" + SteamId; break;
                case "identity": query["openid.identity"] = "https://steamcommunity.com/openid/id/76561198000000999"; break;
            }
        });
        _client.OpenIdValid = what != "verify";

        var completed = await service.CompleteOpenIdAsync("steam", answer, started.State, Ip, CancellationToken.None);

        Assert.Equal(AccountErrors.ProviderFailed, completed.Error);
        Assert.False(_client.PersonaAsked);
    }

    [Fact]
    public async Task Steam_AStateThatIsNotTheBrowsersIsRefused_BeforeSteamIsAsked()
    {
        AllowSteam();
        var service = Service();
        var started = service.Start("steam").Value;

        var completed = await service.CompleteOpenIdAsync("steam", SteamReturn(started.State), "another-browser", Ip, CancellationToken.None);

        Assert.Equal(AccountErrors.InvalidState, completed.Error);
        Assert.Equal(0, _client.OpenIdChecks);
    }

    [Fact]
    public async Task Steam_TheStateInTheAnswerHasToBeTheBrowsersToo()
    {
        AllowSteam();
        var service = Service();
        var started = service.Start("steam").Value;
        var other = service.Start("steam").Value;

        var completed = await service.CompleteOpenIdAsync("steam", SteamReturn(other.State), started.State, Ip, CancellationToken.None);

        Assert.Equal(AccountErrors.InvalidState, completed.Error);
    }

    [Fact]
    public async Task Steam_AnAnswerWorksOnce()
    {
        AllowSteam();
        var service = Service();
        var started = service.Start("steam").Value;
        await service.CompleteOpenIdAsync("steam", SteamReturn(started.State), started.State, Ip, CancellationToken.None);

        var again = await service.CompleteOpenIdAsync("steam", SteamReturn(started.State), started.State, Ip, CancellationToken.None);

        Assert.Equal(AccountErrors.InvalidState, again.Error);
    }

    [Fact]
    public async Task Steam_TheOAuthCompletionDoesNotWorkForIt_AndTheOpenIdOneNotForOthers()
    {
        AllowSteam();
        var service = Service();
        var steam = service.Start("steam").Value;
        var github = service.Start("github").Value;

        var oauthForSteam = await service.CompleteAsync("steam", "code", steam.State, steam.State, Ip, CancellationToken.None);
        var openIdForGitHub = await service.CompleteOpenIdAsync("github", SteamReturn(github.State), github.State, Ip, CancellationToken.None);

        Assert.Equal(AccountErrors.UnknownProvider, oauthForSteam.Error);
        Assert.Equal(AccountErrors.UnknownProvider, openIdForGitHub.Error);
    }

    [Fact]
    public void KindOf_TellsTheControllerWhichWayAProviderSignsIn()
    {
        AllowSteam();
        var service = Service();

        Assert.Equal(OAuthKind.OpenId, service.KindOf("steam"));
        Assert.Equal(OAuthKind.OAuth, service.KindOf("github"));
        Assert.Null(service.KindOf("myspace"));
    }

    [Fact]
    public async Task Steam_APlayerWhoCancelledAtSteamIsToldSo_NothingIsAsked()
    {
        AllowSteam();
        var service = Service();
        var started = service.Start("steam").Value;

        var completed = await service.CompleteOpenIdAsync("steam", SteamReturn(started.State, query => query["openid.mode"] = "cancel"), started.State, Ip, CancellationToken.None);

        Assert.Equal(AccountErrors.Cancelled, completed.Error);
        Assert.Equal(0, _client.OpenIdChecks);
    }

    [Fact]
    public void ACredentialWithSpacesAroundItIsTrimmed_SoAStrayOneNeverMakesTheKeysWrong()
    {
        var credentials = new OAuthCredentials { ClientId = " the-id\r\n", ClientSecret = "\tthe-secret  \n" };

        Assert.Equal("the-id", credentials.ClientId);
        Assert.Equal("the-secret", credentials.ClientSecret);
        Assert.True(credentials.IsComplete);
    }

    [Fact]
    public void ACredentialThatIsOnlySpacesIsNotComplete()
    {
        var credentials = new OAuthCredentials { ClientId = "id", ClientSecret = "   " };

        Assert.False(credentials.IsComplete);
        Assert.Equal(string.Empty, credentials.ClientSecret);
    }

    private Account NewPlayer(string name = "Vini", int tag = 9291)
        => _accounts.Register(name, tag, null, Ip).Value.Account;

    [Fact]
    public async Task Link_AddsTheSocialAccountToTheAccountThatStartedIt_AndMakesNoTicket()
    {
        var service = Service();
        var player = NewPlayer();
        var started = service.StartLink("github", player.Id).Value;

        var completed = await service.CompleteAsync("github", "code", started.State, started.State, Ip, CancellationToken.None);

        Assert.True(completed.Succeeded, completed.Error);
        Assert.Equal("github", completed.Value.LinkedProvider);
        Assert.Null(completed.Value.Ticket);
        Assert.Equal(new[] { "github" }, _accounts.SignInWithExternal("github", "583231", null, null, Ip).Value.Account.Logins.Select(login => login.Provider));
    }

    [Fact]
    public async Task Link_TheAccountNowSignsInWithTheProvider()
    {
        var service = Service();
        var player = NewPlayer();
        var linking = service.StartLink("github", player.Id).Value;
        await service.CompleteAsync("github", "code", linking.State, linking.State, Ip, CancellationToken.None);

        var signing = service.Start("github").Value;
        var completed = await service.CompleteAsync("github", "code", signing.State, signing.State, Ip, CancellationToken.None);

        Assert.Equal(player.Id, service.Redeem(completed.Value).Value.Account.Id);
    }

    [Fact]
    public async Task Link_ASocialAccountOfAnotherPlayerIsRefused_AndNothingIsLinked()
    {
        var service = Service();
        var owner = NewPlayer("Bob", 1);
        _accounts.LinkExternal(owner.Id, "github", "583231");
        var me = NewPlayer("Ana", 2);
        var started = service.StartLink("github", me.Id).Value;

        var completed = await service.CompleteAsync("github", "code", started.State, started.State, Ip, CancellationToken.None);

        Assert.Equal(AccountErrors.AlreadyLinked, completed.Error);
    }

    [Fact]
    public async Task Link_StillNeedsTheBrowsersState_AndWorksOnce()
    {
        var service = Service();
        var player = NewPlayer();
        var started = service.StartLink("github", player.Id).Value;

        var wrong = await service.CompleteAsync("github", "code", started.State, "another", Ip, CancellationToken.None);
        var first = await service.CompleteAsync("github", "code", started.State, started.State, Ip, CancellationToken.None);
        var again = await service.CompleteAsync("github", "code", started.State, started.State, Ip, CancellationToken.None);

        Assert.Equal(AccountErrors.InvalidState, wrong.Error);
        Assert.True(first.Succeeded);
        Assert.Equal(AccountErrors.InvalidState, again.Error);
    }

    [Theory]
    [InlineData("myspace")]
    [InlineData("google")]
    [InlineData(null)]
    public void StartLink_AnUnknownOrUnconfiguredProviderIsRefused(string? provider)
        => Assert.Equal(AccountErrors.UnknownProvider, Service().StartLink(provider, Guid.NewGuid()).Error);

    [Fact]
    public async Task Link_Steam_WorksLikeTheOthers()
    {
        AllowSteam();
        var service = Service();
        var player = NewPlayer();
        var started = service.StartLink("steam", player.Id).Value;

        var completed = await service.CompleteOpenIdAsync("steam", SteamReturn(started.State), started.State, Ip, CancellationToken.None);

        Assert.True(completed.Succeeded, completed.Error);
        Assert.Equal("steam", completed.Value.LinkedProvider);
        Assert.Equal(player.Id, _accounts.SignInWithExternal("steam", SteamId, null, null, Ip).Value.Account.Id);
    }
}
