using System.Text.Json;
using Application.Manager.Api.Rooms;
using Application.Manager.Api.Telemetry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Application.Manager.Api.Controllers;

/// <summary>
/// How the game runs on a player's device, told by the page itself when it is opened with <c>?perf</c>. Anyone may send a report (a guest has no
/// account to ask for); only an administrator reads them. A report has a <c>session</c> (a random id of the page) and whatever numbers the meter has.
/// </summary>
[Route("telemetry")]
[ApiController]
[Produces("application/json")]
public sealed class TelemetryController(PerfReportStore store, ILogger<TelemetryController> logger) : Controller
{
    private const int MaxSessionLength = 64;
    private const int MaxReportBytes = 8 * 1024;

    [HttpPost("perf")]
    [AllowAnonymous]
    [RequestSizeLimit(2 * MaxReportBytes)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Receive([FromBody] JsonElement report)
    {
        var session = SessionOf(report);
        if (session is null) return BadRequest(new { error = "invalidSession" });
        var text = report.GetRawText();
        if (text.Length > MaxReportBytes) return BadRequest(new { error = "reportTooBig" });

        store.Add(session, report);
        logger.LogInformation("Perf {Session} {Report}", session, text);
        return NoContent();
    }

    /// <summary>The newest reports, oldest first; <c>session</c> keeps only one page's.</summary>
    [HttpGet("perf")]
    [AdminAuth]
    [ProducesResponseType(typeof(IEnumerable<PerfReport>), StatusCodes.Status200OK)]
    public IActionResult Reports([FromQuery] string? session = null, [FromQuery] int take = 100)
        => Ok(store.Latest(session, Math.Clamp(take, 1, 500)));

    private static string? SessionOf(JsonElement report)
    {
        if (report.ValueKind != JsonValueKind.Object) return null;
        if (!report.TryGetProperty("session", out var property) || property.ValueKind != JsonValueKind.String) return null;
        var session = property.GetString();
        return string.IsNullOrWhiteSpace(session) || session.Length > MaxSessionLength ? null : session;
    }
}
