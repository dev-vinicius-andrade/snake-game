using System.Net.Http.Headers;
using Application.Manager.Api.Accounts;
using Library.Commons.Api.Attributes.Filters;
using Library.Commons.Game.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Application.Manager.Api.Controllers;

/// <summary>What a game server saw in one life of a signed-in player. Keys the manager does not know are ignored.</summary>
public sealed class StatsReportRequest
{
    /// <summary>A new guid per report: a report already applied is acknowledged without being applied again.</summary>
    public Guid? ReportId { get; set; }

    /// <summary>How much the life added, by <see cref="StatKeys"/> counter key; 0 to 1 000 000 each.</summary>
    public Dictionary<string, long>? Counters { get; set; }

    /// <summary>The best of the life, by <see cref="StatKeys"/> maximum key; 0 to 10 000 000 each.</summary>
    public Dictionary<string, long>? Maxima { get; set; }
}

public sealed class StatsReportResponse
{
    /// <summary>The ids of the achievements the report unlocked.</summary>
    public List<string> Unlocked { get; set; } = [];

    public static implicit operator StatsReportResponse(UnlockedAchievements unlocked) => new() { Unlocked = [.. unlocked.Ids] };
}

public sealed class UnlockedAchievementResponse
{
    public string Id { get; set; } = null!;
    public DateTimeOffset UnlockedAt { get; set; }

    public static implicit operator UnlockedAchievementResponse(UnlockedAchievement unlocked) => new()
    {
        Id = unlocked.Id,
        UnlockedAt = unlocked.UnlockedAt,
    };
}

public sealed class ProfileResponse
{
    public AccountResponse Account { get; set; } = null!;
    /// <summary>Every <see cref="StatKeys"/> key, 0 when never reported.</summary>
    public Dictionary<string, long> Stats { get; set; } = [];
    /// <summary>Earliest unlocked first.</summary>
    public List<UnlockedAchievementResponse> Achievements { get; set; } = [];

    public static implicit operator ProfileResponse(AccountProfile profile) => new()
    {
        Account = profile.Account,
        Stats = new Dictionary<string, long>(profile.Stats),
        Achievements = profile.Achievements.Select(unlocked => (UnlockedAchievementResponse)unlocked).ToList(),
    };
}

public sealed class AchievementResponse
{
    public string Id { get; set; } = null!;
    /// <summary>A game-icons.net name such as game-icons:snake.</summary>
    public string Icon { get; set; } = null!;
    /// <summary>The <see cref="StatKeys"/> key that unlocks it.</summary>
    public string Stat { get; set; } = null!;
    /// <summary>Unlocked when the stat is at least this.</summary>
    public long Threshold { get; set; }

    public static implicit operator AchievementResponse(AchievementDefinition definition) => new()
    {
        Id = definition.Id,
        Icon = definition.Icon,
        Stat = definition.Stat,
        Threshold = definition.Threshold,
    };
}

/// <summary>
/// Stats and achievements of signed-in players (docs/ACHIEVEMENTS.md): a game server reports a life (api key), a player reads its profile
/// (bearer device token), anyone lists the achievements.
/// </summary>
[ApiController]
[AllowAnonymous]
[Produces("application/json")]
public sealed class AccountStatsController(AchievementService achievements, AccountService accounts) : Controller
{
    private const string BearerScheme = "Bearer";

    [HttpPost("accounts/{id:guid}/stats")]
    [ApiKeyAuth]
    [ProducesResponseType(typeof(StatsReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status404NotFound)]
    public IActionResult Report(Guid id, [FromBody] StatsReportRequest request)
    {
        var result = achievements.Report(id, request.ReportId, request.Counters, request.Maxima);
        return result.Succeeded ? Ok((StatsReportResponse)result.Value) : Refusal(result.Error);
    }

    [HttpGet("accounts/me/profile")]
    [ProducesResponseType(typeof(ProfileResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(AccountErrorResponse), StatusCodes.Status401Unauthorized)]
    public IActionResult Profile()
    {
        var account = accounts.Authenticate(BearerToken());
        var profile = account is null ? null : achievements.Profile(account.Id);
        return profile is null ? Refusal(AccountErrors.InvalidToken) : Ok((ProfileResponse)profile);
    }

    [HttpGet("achievements")]
    [ProducesResponseType(typeof(List<AchievementResponse>), StatusCodes.Status200OK)]
    public IActionResult ListAchievements() => Ok(achievements.Definitions.Select(definition => (AchievementResponse)definition).ToList());

    private ObjectResult Refusal(string error) => StatusCode(StatusFor(error), new AccountErrorResponse { Error = error });

    private static int StatusFor(string error) => error switch
    {
        StatsErrors.UnknownAccount => StatusCodes.Status404NotFound,
        AccountErrors.InvalidToken => StatusCodes.Status401Unauthorized,
        _ => StatusCodes.Status400BadRequest,
    };

    private string? BearerToken()
    {
        var header = Request.Headers.Authorization.ToString();
        return AuthenticationHeaderValue.TryParse(header, out var value) && string.Equals(value.Scheme, BearerScheme, StringComparison.OrdinalIgnoreCase)
            ? value.Parameter
            : null;
    }
}
