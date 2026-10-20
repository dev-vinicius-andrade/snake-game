using System.Text.Json;
using System.Web;
using Application.Manager.Api.Accounts;
using Application.Manager.Api.Accounts.OAuth;
using Application.Manager.Api.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Time.Testing;

namespace Application.Manager.Api.Tests;

public sealed class OAuthControllerTests
{
    private const string Ui = "https://snake.example.com";
    private const string Api = "https://snake.example.com/api";
    private const string Ip = "203.0.113.7";
    private const string CookieName = "snake_oauth_state";

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));
    private readonly OAuthOptions _options = new()
    {
        PublicApiUrl = Api,
        PublicUiUrl = Ui,
        Providers = { ["github"] = new OAuthCredentials { ClientId = "gh-id", ClientSecret = "gh-secret" } },
    };
    private readonly Fake _client = new();
    private readonly OAuthService _service;

    private sealed class Fake : IOAuthClient
    {
        public int Exchanges { get; private set; }

        public Task<bool> VerifyOpenIdAsync(OAuthProviderInfo provider, IReadOnlyDictionary<string, string> answer, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<string?> GetSteamPersonaAsync(OAuthProviderInfo provider, string apiKey, string steamId, CancellationToken cancellationToken) => Task.FromResult<string?>("Gaben");

        public Task<string?> ExchangeCodeAsync(OAuthProviderInfo provider, OAuthCredentials credentials, string code, string redirectUri, string codeVerifier, CancellationToken cancellationToken)
        {
            Exchanges++;
            return Task.FromResult<string?>("token");
        }

        public Task<JsonElement?> GetUserAsync(OAuthProviderInfo provider, string accessToken, CancellationToken cancellationToken)
            => Task.FromResult<JsonElement?>(JsonDocument.Parse("""{ "id": 7, "login": "octocat", "name": "The Octocat" }""").RootElement);
    }

    public OAuthControllerTests()
    {
        var accounts = new AccountService(new FileAccountStore(null), AccountThrottles.Create(_time), null, new RecoveryPhrase(), new SecretHasher(1), _time);
        _service = new OAuthService(_options, _client, accounts, _time);
    }

    private OAuthController Controller(string? stateCookie = null)
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = System.Net.IPAddress.Parse(Ip);
        if (stateCookie is not null)
            http.Request.Headers.Cookie = $"{CookieName}={stateCookie}";
        return new OAuthController(_service, _options) { ControllerContext = new ControllerContext { HttpContext = http } };
    }

    private static string RedirectOf(IActionResult result) => Assert.IsType<RedirectResult>(result).Url;

    private static IDictionary<string, string?> QueryOf(string url)
    {
        var query = HttpUtility.ParseQueryString(new Uri(url, UriKind.RelativeOrAbsolute).IsAbsoluteUri ? new Uri(url).Query : url[(url.IndexOf('?') + 1)..]);
        return query.AllKeys.ToDictionary(key => key!, key => query[key]);
    }

    private static string SetCookie(OAuthController controller) => controller.Response.Headers.SetCookie.ToString();

    [Fact]
    public void Providers_ListsTheOnesWithKeys_AndNothingSecret()
    {
        var result = Assert.IsType<OkObjectResult>(Controller().Providers());

        var json = JsonSerializer.Serialize(result.Value, WebJson);
        Assert.Equal("""[{"id":"github","title":"GitHub"}]""", json);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Start_SendsTheBrowserToTheProvider_AndKeepsTheStateInAProtectedCookie()
    {
        var controller = Controller();

        var url = RedirectOf(controller.Start("github"));

        Assert.StartsWith("https://github.com/login/oauth/authorize?", url);
        var state = QueryOf(url)["state"];
        var cookie = SetCookie(controller);
        Assert.Contains($"{CookieName}={state}", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=600", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
    }

    [Fact]
    public void Start_OverPlainHttp_TheCookieIsNotSecure_SoLocalDevelopmentWorks()
    {
        _options.PublicApiUrl = "http://localhost:60510";
        var controller = Controller();

        controller.Start("github");

        Assert.DoesNotContain("secure", SetCookie(controller), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Start_AnUnknownProviderSendsTheBrowserBackToTheSiteWithTheReason()
    {
        var url = RedirectOf(Controller().Start("myspace"));

        Assert.Equal($"{Ui}/account/oauth?error=unknownProvider", url);
    }

    [Fact]
    public async Task Callback_TradesTheCodeAndSendsTheBrowserBackWithATicket_NotWithTheToken()
    {
        var starter = Controller();
        var state = QueryOf(RedirectOf(starter.Start("github")))["state"]!;
        var controller = Controller(stateCookie: state);

        var url = RedirectOf(await controller.Callback("github", "the-code", state, null, CancellationToken.None));

        Assert.StartsWith($"{Ui}/account/oauth?ticket=", url);
        var ticket = QueryOf(url)["ticket"];
        Assert.False(string.IsNullOrEmpty(ticket));
        Assert.Contains($"{CookieName}=;", SetCookie(controller));
        Assert.Equal("no-store", controller.Response.Headers.CacheControl.ToString());
        Assert.True(_service.Redeem(ticket).Succeeded);
    }

    [Fact]
    public async Task Callback_APlayerWhoRefusedAtTheProviderIsSentBackWithoutAnythingAskedOfIt()
    {
        var controller = Controller(stateCookie: "x");

        var url = RedirectOf(await controller.Callback("github", null, "x", "access_denied", CancellationToken.None));

        Assert.Equal($"{Ui}/account/oauth?error=cancelled", url);
        Assert.Equal(0, _client.Exchanges);
    }

    [Fact]
    public async Task Callback_WithAStateThatIsNotTheBrowsersIsRefused()
    {
        var state = QueryOf(RedirectOf(Controller().Start("github")))["state"]!;

        var url = RedirectOf(await Controller(stateCookie: "someone-elses").Callback("github", "code", state, null, CancellationToken.None));

        Assert.Equal($"{Ui}/account/oauth?error=invalidState", url);
        Assert.Equal(0, _client.Exchanges);
    }

    [Fact]
    public async Task Callback_WithoutTheCookieIsRefused()
    {
        var state = QueryOf(RedirectOf(Controller().Start("github")))["state"]!;

        var url = RedirectOf(await Controller().Callback("github", "code", state, null, CancellationToken.None));

        Assert.Equal($"{Ui}/account/oauth?error=invalidState", url);
    }

    [Fact]
    public async Task Callback_WithoutAPublicSiteAddress_SendsTheBrowserBackByAPath()
    {
        _options.PublicUiUrl = "";

        var url = RedirectOf(await Controller(stateCookie: "x").Callback("github", null, "x", "access_denied", CancellationToken.None));

        Assert.Equal("/account/oauth?error=cancelled", url);
    }

    [Fact]
    public async Task Exchange_GivesTheGameTokenForATicket_Once()
    {
        var state = QueryOf(RedirectOf(Controller().Start("github")))["state"]!;
        var ticket = QueryOf(RedirectOf(await Controller(stateCookie: state).Callback("github", "code", state, null, CancellationToken.None)))["ticket"]!;

        var first = Controller().Exchange(new ExchangeTicketRequest { Ticket = ticket });
        var again = Controller().Exchange(new ExchangeTicketRequest { Ticket = ticket });

        var ok = Assert.IsType<OkObjectResult>(first);
        var body = JsonSerializer.SerializeToElement(ok.Value, WebJson);
        Assert.Equal(new[] { "username", "tag", "display", "displayName", "token" }, body.EnumerateObject().Select(property => property.Name));
        Assert.Equal("The Octocat", body.GetProperty("username").GetString());
        Assert.Equal("The Octocat", body.GetProperty("displayName").GetString());
        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsAssignableFrom<ObjectResult>(again).StatusCode);
    }

    [Fact]
    public void Exchange_ABlankTicketIsRefused()
        => Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsAssignableFrom<ObjectResult>(Controller().Exchange(new ExchangeTicketRequest { Ticket = " " })).StatusCode);

    [Fact]
    public async Task Callback_ForSteam_PassesTheWholeAnswerOn_AndSendsTheBrowserBackWithATicket()
    {
        _options.Providers["steam"] = new OAuthCredentials { ClientSecret = "web-api-key" };
        var starter = Controller();
        var state = QueryOf(RedirectOf(starter.Start("steam")))["openid.return_to"]!.Split("state=")[1];
        var controller = Controller(stateCookie: state);
        controller.Request.QueryString = new QueryString(
            $"?state={state}&openid.ns=http%3A%2F%2Fspecs.openid.net%2Fauth%2F2.0&openid.mode=id_res&openid.op_endpoint=https%3A%2F%2Fsteamcommunity.com%2Fopenid%2Flogin"
            + "&openid.claimed_id=https%3A%2F%2Fsteamcommunity.com%2Fopenid%2Fid%2F76561198000000001&openid.identity=https%3A%2F%2Fsteamcommunity.com%2Fopenid%2Fid%2F76561198000000001"
            + $"&openid.return_to={Uri.EscapeDataString($"{Api}/accounts/oauth/steam/callback?state={state}")}&openid.sig=x");

        var url = RedirectOf(await controller.Callback("steam", null, null, null, CancellationToken.None));

        Assert.StartsWith($"{Ui}/account/oauth?ticket=", url);
    }
}
