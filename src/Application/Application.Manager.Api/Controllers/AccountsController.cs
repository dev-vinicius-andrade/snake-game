using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;
using Application.Manager.Api.Accounts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Application.Manager.Api.Controllers;

public sealed class RegisterAccountRequest
{
    public string? Username { get; set; }
    /// <summary>The tag the player asks for; left out: a random free one.</summary>
    public int? Tag { get; set; }
    /// <summary>The recovery phrase the player chose; left out: a random one.</summary>
    public string? Phrase { get; set; }
    /// <summary>The name other players see; left out: the username. Never the tag.</summary>
    public string? DisplayName { get; set; }
}

public sealed class ChangeHandleRequest
{
    /// <summary>The new username: how the owner signs in, with the tag.</summary>
    public string? Username { get; set; }

    /// <summary>The tag wanted, 0 to 99999. Left out: the account keeps its tag if only the case of the name changes, else a free one is picked.</summary>
    public int? Tag { get; set; }
}

public sealed class ChangeDisplayNameRequest
{
    /// <summary>The name other players see; blank: back to the username.</summary>
    public string? DisplayName { get; set; }
}

public sealed class LoginRequest
{
    public string? Username { get; set; }
    public int? Tag { get; set; }
    public string? Phrase { get; set; }
}

public sealed class RegisteredAccountResponse
{
    public string Username { get; set; } = null!;
    public int Tag { get; set; }
    /// <summary>Name#09291: for the owner only, it is how the owner signs in.</summary>
    public string Display { get; set; } = null!;
    /// <summary>The name other players see.</summary>
    public string DisplayName { get; set; } = null!;
    /// <summary>The social accounts linked, by provider (github, discord...). Nothing of their ids.</summary>
    public IReadOnlyList<string> Linked { get; set; } = [];
    public string Token { get; set; } = null!;
    /// <summary>To show the player once.</summary>
    public string RecoveryPhrase { get; set; } = null!;

    public static implicit operator RegisteredAccountResponse(Registration registration) => new()
    {
        Username = registration.Account.Username,
        Tag = registration.Account.Tag,
        Display = registration.Account.Display,
        DisplayName = registration.Account.PublicName,
        Linked = AccountResponse.Providers(registration.Account),
        Token = registration.Token,
        RecoveryPhrase = registration.RecoveryPhrase,
    };
}

public sealed class SignedInResponse
{
    public string Username { get; set; } = null!;
    public int Tag { get; set; }
    public string Display { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public IReadOnlyList<string> Linked { get; set; } = [];
    public string Token { get; set; } = null!;

    public static implicit operator SignedInResponse(SignIn signIn) => new()
    {
        Username = signIn.Account.Username,
        Tag = signIn.Account.Tag,
        Display = signIn.Account.Display,
        DisplayName = signIn.Account.PublicName,
        Linked = AccountResponse.Providers(signIn.Account),
        Token = signIn.Token,
    };
}

public sealed class AccountResponse
{
    public Guid Id { get; set; }
    public string Username { get; set; } = null!;
    public int Tag { get; set; }
    public string Display { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public IReadOnlyList<string> Linked { get; set; } = [];

    /// <summary>What the account can do: <c>user</c>, and <c>admin</c> when it was granted (docs/ROLES.md).</summary>
    public IReadOnlyList<string> Roles { get; set; } = [Accounts.Roles.User];
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>The providers of the social accounts linked to an account, in order. Only their names: the ids are not even kept.</summary>
    public static IReadOnlyList<string> Providers(Account account) => account.Logins.Select(login => login.Provider).Distinct().Order().ToList();

    public static implicit operator AccountResponse(Account account) => new()
    {
        Id = account.Id,
        Username = account.Username,
        Tag = account.Tag,
        Display = account.Display,
        DisplayName = account.PublicName,
        Linked = Providers(account),
        CreatedAt = account.CreatedAt,
    };
}

public sealed class AvailabilityResponse
{
    public bool Available { get; set; }
    /// <summary>The username as it would be kept.</summary>
    public string Username { get; set; } = null!;

    public static implicit operator AvailabilityResponse(UsernameAvailability availability) => new()
    {
        Available = availability.Available,
        Username = availability.Username,
    };
}

public sealed class AccountErrorResponse
{
    /// <summary>One of <see cref="AccountErrors"/>.</summary>
    public string Error { get; set; } = null!;

    /// <summary>Only with <see cref="AccountErrors.Throttled"/>: whole seconds to wait, also sent as the Retry-After header.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? RetryAfterSeconds { get; set; }
}

/// <summary>Player accounts (docs/ACCOUNTS.md): register, sign in on a new device, who am I, sign out, is a name free.</summary>
[Route("accounts")]
[ApiController]
[AllowAnonymous]
[Produces("application/json")]
public sealed class AccountsController(AccountService accounts, IRoleStore? roles = null) : Controller
{
    private const string BearerScheme = "Bearer";

    [HttpPost]
    [ProducesResponseType(typeof(RegisteredAccountResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status429TooManyRequests)]
    public IActionResult Register([FromBody] RegisterAccountRequest request)
    {
        var result = accounts.Register(request.Username, request.Tag, request.Phrase, ClientIp(), request.DisplayName);
        return result.Succeeded
            ? StatusCode(StatusCodes.Status201Created, (RegisteredAccountResponse)result.Value)
            : Refusal(result.Error, result.RetryAfterSeconds);
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(SignedInResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status429TooManyRequests)]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        var result = accounts.Login(request.Username, request.Tag, request.Phrase, ClientIp());
        return result.Succeeded ? Ok((SignedInResponse)result.Value) : Refusal(result.Error, result.RetryAfterSeconds);
    }

    [HttpGet("me")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        var account = accounts.Authenticate(BearerToken());
        if (account is null) return Refusal(AccountErrors.InvalidToken);
        var response = (AccountResponse)account;
        response.Roles = Roles.Effective(roles?.RolesOf(account.Id) ?? []);
        return Ok(response);
    }

    /// <summary>Changes the username and tag the owner signs in with (<c>Name#09291</c>), if they are free.</summary>
    [HttpPut("me/handle")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status429TooManyRequests)]
    public IActionResult ChangeHandle([FromBody] ChangeHandleRequest request)
    {
        var result = accounts.ChangeHandle(BearerToken(), request.Username, request.Tag);
        return result.Succeeded ? Ok((AccountResponse)result.Value) : Refusal(result.Error, result.RetryAfterSeconds);
    }

    [HttpPut("me/display-name")]
    [ProducesResponseType(typeof(AccountResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status401Unauthorized)]
    public IActionResult ChangeDisplayName([FromBody] ChangeDisplayNameRequest request)
    {
        var result = accounts.ChangeDisplayName(BearerToken(), request.DisplayName);
        return result.Succeeded ? Ok((AccountResponse)result.Value) : Refusal(result.Error);
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status401Unauthorized)]
    public IActionResult Logout() => accounts.Logout(BearerToken()) ? NoContent() : Refusal(AccountErrors.InvalidToken);

    [HttpGet("availability")]
    [ProducesResponseType(typeof(AvailabilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status429TooManyRequests)]
    public IActionResult Availability([FromQuery] string? username, [FromQuery] string? tag)
    {
        var result = accounts.Availability(username, tag, ClientIp());
        return result.Succeeded ? Ok((AvailabilityResponse)result.Value) : Refusal(result.Error, result.RetryAfterSeconds);
    }

    private IActionResult Refusal(string error, int? retryAfterSeconds = null)
    {
        if (retryAfterSeconds is int seconds)
            Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        return StatusCode(StatusFor(error), new AccountErrorResponse { Error = error, RetryAfterSeconds = retryAfterSeconds });
    }

    private static int StatusFor(string error) => error switch
    {
        AccountErrors.NoTagsLeft or AccountErrors.NameTaken => StatusCodes.Status409Conflict,
        AccountErrors.InvalidCredentials or AccountErrors.InvalidToken => StatusCodes.Status401Unauthorized,
        AccountErrors.Throttled => StatusCodes.Status429TooManyRequests,
        _ => StatusCodes.Status400BadRequest,
    };

    /// <summary>The address the connection comes from. X-Forwarded-For is not trusted: the app sets up no forwarded headers handling.</summary>
    private string? ClientIp()
    {
        var address = HttpContext.Connection.RemoteIpAddress;
        return address is { IsIPv4MappedToIPv6: true } ? address.MapToIPv4().ToString() : address?.ToString();
    }

    private string? BearerToken()
    {
        var header = Request.Headers.Authorization.ToString();
        return AuthenticationHeaderValue.TryParse(header, out var value) && string.Equals(value.Scheme, BearerScheme, StringComparison.OrdinalIgnoreCase)
            ? value.Parameter
            : null;
    }
}
