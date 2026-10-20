using Application.Manager.Api.Accounts;
using Application.Manager.Api.Rooms;
using Library.Commons.Game.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Application.Manager.Api.Controllers;

public sealed class AchievementTextRequest
{
    public string? Name { get; set; }
    public string? Description { get; set; }
}

public sealed class AchievementRequest
{
    /// <summary>A game-icons.net name such as <c>game-icons:snake</c>.</summary>
    public string? Icon { get; set; }

    /// <summary>The key of the number that unlocks it (<see cref="StatKeys"/>).</summary>
    public string? Stat { get; set; }

    /// <summary>Unlocked when the stat is at least this.</summary>
    public long Threshold { get; set; }

    /// <summary>Off: nobody can unlock it and it is not listed. What was unlocked stays.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>The name and description by language (<c>en</c>, <c>pt</c>). A language left out shows the site's own text, if it has one.</summary>
    public Dictionary<string, AchievementTextRequest>? Texts { get; set; }
}

/// <summary>The achievements of the game, changed from the admin page (docs/ACHIEVEMENTS.md). For administrators only.</summary>
[Route("admin/achievements")]
[ApiController]
[AdminAuth]
[Produces("application/json")]
public sealed class AdminAchievementsController(IAchievementCatalog catalog) : Controller
{
    /// <summary>Every achievement (the switched off ones too), the stats that can unlock one, and whether they can be changed at all.</summary>
    [HttpGet]
    public IActionResult List() => Ok(new
    {
        achievements = catalog.All().Select(entry => new
        {
            id = entry.Id,
            icon = entry.Icon,
            stat = entry.Stat,
            threshold = entry.Threshold,
            enabled = entry.Enabled,
            texts = (entry.Texts ?? new Dictionary<string, AchievementText>()).ToDictionary(text => text.Key, text => new { name = text.Value.Name, description = text.Value.Description }),
        }),
        stats = StatKeys.Counters.Concat(StatKeys.Maxima),
        writable = catalog.IsWritable,
    });

    /// <summary>Adds the achievement, or changes the one with this id.</summary>
    [HttpPut("{id}")]
    public IActionResult Put(string id, [FromBody] AchievementRequest request)
    {
        var entry = new AchievementEntry(
            id,
            request.Icon ?? "",
            request.Stat ?? "",
            request.Threshold,
            request.Enabled,
            request.Texts?.ToDictionary(text => text.Key, text => new AchievementText((text.Value?.Name ?? "").Trim(), (text.Value?.Description ?? "").Trim())));
        if (AchievementRules.Validate(entry) is { } invalid)
            return StatusCode(StatusCodes.Status400BadRequest, new { error = invalid });
        return catalog.Save(entry) switch
        {
            AchievementChange.ReadOnly => StatusCode(StatusCodes.Status409Conflict, new { error = AchievementErrors.ReadOnly }),
            _ => Ok(new { id = entry.Id }),
        };
    }

    /// <summary>Removes it. One that somebody unlocked cannot be removed (switch it off instead).</summary>
    [HttpDelete("{id}")]
    public IActionResult Delete(string id) => catalog.Delete(id) switch
    {
        AchievementChange.Deleted => NoContent(),
        AchievementChange.NotFound => StatusCode(StatusCodes.Status404NotFound, new { error = AchievementErrors.NotFound }),
        AchievementChange.InUse => StatusCode(StatusCodes.Status409Conflict, new { error = AchievementErrors.InUse }),
        _ => StatusCode(StatusCodes.Status409Conflict, new { error = AchievementErrors.ReadOnly }),
    };
}
