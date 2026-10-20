using System.Reflection;
using System.Text.Json;
using Application.Manager.Api.Accounts;
using Application.Manager.Api.Controllers;
using Application.Manager.Api.Rooms;
using Library.Commons.Game.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Application.Manager.Api.Tests;

public sealed class AdminAchievementsControllerTests
{
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private static AchievementRequest Request(string icon = "game-icons:sparkles", string stat = StatKeys.Foods, long threshold = 10, bool enabled = true) => new()
    {
        Icon = icon,
        Stat = stat,
        Threshold = threshold,
        Enabled = enabled,
        Texts = new Dictionary<string, AchievementTextRequest> { ["en"] = new() { Name = "Snacker", Description = "Eat 10 fruits." } },
    };

    private static int Status(IActionResult result) => Assert.IsAssignableFrom<IStatusCodeActionResult>(result).StatusCode!.Value;

    private static string ErrorOf(IActionResult result)
        => JsonSerializer.SerializeToElement(Assert.IsAssignableFrom<ObjectResult>(result).Value, WebJson).GetProperty("error").GetString()!;

    [Fact]
    public void TheController_IsForAdminsOnly()
        => Assert.NotNull(typeof(AdminAchievementsController).GetCustomAttribute<AdminAuth>());

    [Fact]
    public void List_GivesEveryAchievementEvenSwitchedOffOnes_TheStatsThatCanUnlockThem_AndWhetherTheyCanBeChanged()
    {
        var catalog = new MemoryAchievementCatalog();
        catalog.Save(new AchievementEntry("on", "game-icons:snake", StatKeys.Games, 1));
        catalog.Save(new AchievementEntry("off", "game-icons:snake", StatKeys.Games, 2, false));

        var ok = Assert.IsType<OkObjectResult>(new AdminAchievementsController(catalog).List());

        var body = JsonSerializer.SerializeToElement(ok.Value, WebJson);
        Assert.Equal(new[] { "on", "off" }, body.GetProperty("achievements").EnumerateArray().Select(entry => entry.GetProperty("id").GetString()));
        Assert.Equal(StatKeys.Counters.Concat(StatKeys.Maxima), body.GetProperty("stats").EnumerateArray().Select(stat => stat.GetString()));
        Assert.True(body.GetProperty("writable").GetBoolean());
    }

    [Fact]
    public void Put_AddsOrChanges_AndAnswersWithWhatWasSaved()
    {
        var catalog = new MemoryAchievementCatalog();
        var controller = new AdminAchievementsController(catalog);

        var added = controller.Put("snacker", Request());
        var changed = controller.Put("snacker", Request(threshold: 25, enabled: false));

        Assert.Equal(StatusCodes.Status200OK, Status(added));
        Assert.Equal(StatusCodes.Status200OK, Status(changed));
        var saved = Assert.Single(catalog.All());
        Assert.Equal((25L, false), (saved.Threshold, saved.Enabled));
        Assert.Equal("Snacker", saved.Texts!["en"].Name);
    }

    [Theory]
    [InlineData("Bad Id", "game-icons:snake", "games", 1L, AchievementErrors.InvalidId)]
    [InlineData("good-id", "snake", "games", 1L, AchievementErrors.InvalidIcon)]
    [InlineData("good-id", "game-icons:snake", "nothing", 1L, AchievementErrors.InvalidStat)]
    [InlineData("good-id", "game-icons:snake", "games", 0L, AchievementErrors.InvalidThreshold)]
    public void Put_SomethingThatIsNotValid_Is400_AndNothingIsSaved(string id, string icon, string stat, long threshold, string error)
    {
        var catalog = new MemoryAchievementCatalog();

        var result = new AdminAchievementsController(catalog).Put(id, Request(icon, stat, threshold));

        Assert.Equal(StatusCodes.Status400BadRequest, Status(result));
        Assert.Equal(error, ErrorOf(result));
        Assert.Empty(catalog.All());
    }

    [Fact]
    public void Put_AndDelete_WhenTheCatalogIsTheCode_Are409ReadOnly()
    {
        var controller = new AdminAchievementsController(new CodeAchievementCatalog());

        var put = controller.Put("snacker", Request());
        var delete = controller.Delete("hunter");

        Assert.Equal(StatusCodes.Status409Conflict, Status(put));
        Assert.Equal("readOnly", ErrorOf(put));
        Assert.Equal(StatusCodes.Status409Conflict, Status(delete));
    }

    [Fact]
    public void Delete_RemovesIt_NotFoundIs404_AndOneInUseIs409()
    {
        var catalog = new MemoryAchievementCatalog();
        catalog.Save(new AchievementEntry("snacker", "game-icons:sparkles", StatKeys.Foods, 10));
        var controller = new AdminAchievementsController(catalog);

        Assert.Equal(StatusCodes.Status204NoContent, Status(controller.Delete("snacker")));
        Assert.Equal(StatusCodes.Status404NotFound, Status(controller.Delete("snacker")));

        catalog.MarkInUse("held");
        catalog.Save(new AchievementEntry("held", "game-icons:snake", StatKeys.Games, 1));
        var inUse = controller.Delete("held");
        Assert.Equal(StatusCodes.Status409Conflict, Status(inUse));
        Assert.Equal("inUse", ErrorOf(inUse));
    }
}
