using System.Text.Json;
using Application.Manager.Api.Gameplay;
using Application.Manager.Api.Rooms;
using Domain.Game.Rules;
using Library.Commons.Api.Attributes.Filters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Application.Manager.Api.Controllers;

/// <summary>The gameplay config as game servers fetch it.</summary>
[Route("gameplay")]
[ApiController]
public class GameplayController(GameplayConfigStore store) : Controller
{
    [HttpGet]
    [ApiKeyAuth]
    public IActionResult Get() => Json(store.Current);

    /// <summary>What the perks are and do, so the site can show a snake's perks as icons with tooltips. Nothing secret.</summary>
    [HttpGet("perks")]
    [AllowAnonymous]
    public IActionResult Perks()
    {
        var current = store.Current;
        // Only what a player may see: no scripts and no room names.
        var perks = current.Config.Perks.Enabled
            ? current.Config.Perks.Perks.OrderBy(perk => perk.FoodsRequired)
                .Select(perk => new { id = perk.Id, name = perk.Name, foodsRequired = perk.FoodsRequired, effects = perk.AllEffects(), icon = perk.Icon, description = perk.Description })
                .ToList()
            : [];
        return Json(new { revision = current.Revision, perks });
    }

    private new JsonResult Json(object value) => new(value, GameplayConfig.Json);
}

/// <summary>The administrator's view of the gameplay config: read it and change it.</summary>
[Route("admin/gameplay")]
[ApiController]
[AdminKeyAuth]
public class AdminGameplayController(GameplayConfigStore store) : Controller
{
    /// <summary>The current config, the defaults to go back to, and the effects a perk can have.</summary>
    [HttpGet]
    public IActionResult Get() => Json(new
    {
        current = store.Current,
        defaults = GameplayConfig.Defaults,
        effects = Enum.GetNames<PerkEffect>(),
    });

    /// <summary>Whether a script would be accepted: the editor asks while the administrator writes. Nothing is saved.</summary>
    [HttpPost("check-script")]
    public async Task<IActionResult> CheckScript(CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(Request.Body);
        var script = await reader.ReadToEndAsync(cancellationToken);
        return Json(PerkScriptChecker.Check(script));
    }

    [HttpPut]
    public async Task<IActionResult> Put(CancellationToken cancellationToken = default)
    {
        GameplayConfig? config;
        try
        {
            config = await JsonSerializer.DeserializeAsync<GameplayConfig>(Request.Body, GameplayConfig.Json, cancellationToken);
        }
        catch (JsonException)
        {
            return BadRequest(new { error = "That is not a valid gameplay config." });
        }
        if (config is null) return BadRequest(new { error = "The gameplay config is missing." });
        try
        {
            return Json(store.Update(config));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    private new JsonResult Json(object value) => new(value, GameplayConfig.Json);
}
