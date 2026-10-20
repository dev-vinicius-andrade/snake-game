using Application.Manager.Api.Accounts;
using Application.Manager.Api.Accounts.OAuth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Application.Manager.Api.Controllers;

public sealed class ExchangeTicketRequest
{
    public string? Ticket { get; set; }
}

public sealed class OAuthProviderResponse
{
    public string Id { get; set; } = null!;
    public string Title { get; set; } = null!;
}

/// <summary>
/// Signing in with a social account (docs/ACCOUNTS.md). The site sends the browser to <c>start</c>, the provider sends it back to
/// <c>callback</c>, and the manager sends it on to the site with a one time ticket that <c>exchange</c> trades for the device token. The
/// token never travels in an address.
/// </summary>
[Route("accounts/oauth")]
[ApiController]
[AllowAnonymous]
public sealed class OAuthController(OAuthService oauth, OAuthOptions options) : Controller
{
    /// <summary>Holds the state of a sign in that is under way, so only the browser that started it can finish it.</summary>
    public const string StateCookie = "snake_oauth_state";

    private static readonly TimeSpan CookieLifetime = TimeSpan.FromMinutes(10);

    /// <summary>The providers the site may offer: only those with keys. Nothing secret.</summary>
    [HttpGet("providers")]
    [ProducesResponseType(typeof(IEnumerable<OAuthProviderResponse>), StatusCodes.Status200OK)]
    public IActionResult Providers()
        => Ok(oauth.Providers().Select(provider => new OAuthProviderResponse { Id = provider.Id, Title = provider.Title }));

    /// <summary>Sends the browser to the provider. Meant to be followed by the browser itself, not called from the page's code.</summary>
    [HttpGet("{provider}/start")]
    public IActionResult Start(string provider)
    {
        NoStore();
        var started = oauth.Start(provider);
        if (!started.Succeeded)
            return Back(("error", started.Error));
        Response.Cookies.Append(StateCookie, started.Value.State, StateCookieOptions(CookieLifetime));
        return Redirect(started.Value.Url);
    }

    /// <summary>Where the provider sends the browser back to. Always answers with a redirect to the site: a ticket, or the reason it failed.</summary>
    [HttpGet("{provider}/callback")]
    public async Task<IActionResult> Callback(string provider, [FromQuery] string? code, [FromQuery] string? state, [FromQuery] string? error, CancellationToken cancellationToken)
    {
        NoStore();
        var browserState = Request.Cookies[StateCookie];
        Response.Cookies.Append(StateCookie, string.Empty, StateCookieOptions(TimeSpan.Zero));
        if (!string.IsNullOrEmpty(error))
            return Back(("error", "cancelled"));
        // Steam answers with a long signed OpenID answer, not a code: the whole address is needed.
        var completed = oauth.KindOf(provider) == OAuthKind.OpenId
            ? await oauth.CompleteOpenIdAsync(provider, Request.Query.ToDictionary(pair => pair.Key, pair => pair.Value.ToString()), browserState, ClientIp(), cancellationToken)
            : await oauth.CompleteAsync(provider, code, state, browserState, ClientIp(), cancellationToken);
        return completed.Succeeded ? Back(("ticket", completed.Value)) : Back(("error", completed.Error));
    }

    /// <summary>Trades the ticket for the game's device token, once.</summary>
    [HttpPost("exchange")]
    [ProducesResponseType(typeof(SignedInResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status401Unauthorized)]
    public IActionResult Exchange([FromBody] ExchangeTicketRequest request)
    {
        NoStore();
        var result = oauth.Redeem(request.Ticket);
        return result.Succeeded
            ? Ok((SignedInResponse)result.Value)
            : StatusCode(StatusCodes.Status401Unauthorized, new AccountErrorResponse { Error = result.Error });
    }

    /// <summary>The site's page for the end of a sign in. The address comes from the settings, never from the request.</summary>
    private IActionResult Back((string Name, string Value) outcome)
        => Redirect($"{options.PublicUiUrl.TrimEnd('/')}/account/oauth?{outcome.Name}={Uri.EscapeDataString(outcome.Value)}");

    private CookieOptions StateCookieOptions(TimeSpan lifetime) => new()
    {
        HttpOnly = true,
        // The provider sends the browser back with a plain link, which a Lax cookie goes along with.
        SameSite = SameSiteMode.Lax,
        Secure = options.PublicApiUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase),
        MaxAge = lifetime,
        Path = "/",
    };

    private void NoStore() => Response.Headers.CacheControl = "no-store";

    private string? ClientIp()
    {
        var address = HttpContext.Connection.RemoteIpAddress;
        return address is { IsIPv4MappedToIPv6: true } ? address.MapToIPv4().ToString() : address?.ToString();
    }
}
