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
    public string Display { get; set; } = null!;
    public string Token { get; set; } = null!;
    /// <summary>To show the player once.</summary>
    public string RecoveryPhrase { get; set; } = null!;

    public static implicit operator RegisteredAccountResponse(Registration registration) => new()
    {
        Username = registration.Account.Username,
        Tag = registration.Account.Tag,
        Display = registration.Account.Display,
        Token = registration.Token,
        RecoveryPhrase = registration.RecoveryPhrase,
    };
}

public sealed class SignedInResponse
{
    public string Username { get; set; } = null!;
    public int Tag { get; set; }
    public string Display { get; set; } = null!;
    public string Token { get; set; } = null!;

    public static implicit operator SignedInResponse(SignIn signIn) => new()
    {
        Username = signIn.Account.Username,
        Tag = signIn.Account.Tag,
        Display = signIn.Account.Display,
        Token = signIn.Token,
    };
}

public sealed class AccountResponse
{
    public Guid Id { get; set; }
    public string Username { get; set; } = null!;
    public int Tag { get; set; }
    public string Display { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; }

    public static implicit operator AccountResponse(Account account) => new()
    {
        Id = account.Id,
        Username = account.Username,
        Tag = account.Tag,
        Display = account.Display,
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
public sealed class AccountsController(AccountService accounts) : Controller
{
    private const string BearerScheme = "Bearer";

    [HttpPost]
    [ProducesResponseType(typeof(RegisteredAccountResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status429TooManyRequests)]
    public IActionResult Register([FromBody] RegisterAccountRequest request)
    {
        var result = accounts.Register(request.Username, request.Tag, request.Phrase, ClientIp());
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
        return account is null ? Refusal(AccountErrors.InvalidToken) : Ok((AccountResponse)account);
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
        AccountErrors.NoTagsLeft => StatusCodes.Status409Conflict,
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
