using Application.Manager.Api.Persistence;
using Library.Commons.Game.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Manager.Api.Tests.Persistence;

public sealed class AchievementCatalogTests : IDisposable
{
    private readonly PersistenceTestHost _host = new();

    public void Dispose() => _host.Dispose();

    private static readonly IReadOnlyList<AchievementDefinition> Definitions =
    [
        new("first-steps", "game-icons:snake", "games", 1),
        new("hunter", "game-icons:crosshair", "kills", 25),
    ];

    private List<AchievementRow> Rows()
    {
        using var context = _host.Factory.CreateDbContext();
        return context.Achievements.AsNoTracking().OrderBy(row => row.SortOrder).ToList();
    }

    [Fact]
    public void Sync_PutsEveryDefinitionInTheCatalog_InTheOrderOfTheList()
    {
        AchievementCatalog.Sync(_host.Factory, Definitions);

        Assert.Equal(new[] { "first-steps", "hunter" }, Rows().Select(row => row.Id));
        Assert.Equal(new[] { 0, 1 }, Rows().Select(row => row.SortOrder));
    }

    [Fact]
    public void Sync_TheDefaultsOfTheGame_AllGoIn()
    {
        AchievementCatalog.Sync(_host.Factory, Achievements.Defaults);

        Assert.Equal(Achievements.Defaults.Select(definition => definition.Id), Rows().Select(row => row.Id));
    }

    [Fact]
    public void Sync_RunTwice_ChangesNothing()
    {
        AchievementCatalog.Sync(_host.Factory, Definitions);
        AchievementCatalog.Sync(_host.Factory, Definitions);

        Assert.Equal(2, Rows().Count);
    }

    [Fact]
    public void Sync_ADefinitionThatChanged_IsUpdatedInPlace()
    {
        AchievementCatalog.Sync(_host.Factory, Definitions);

        AchievementCatalog.Sync(_host.Factory, [new("first-steps", "game-icons:snake", "games", 3), Definitions[1]]);

        Assert.Equal(3, Rows().Single(row => row.Id == "first-steps").Threshold);
    }

    [Fact]
    public void Sync_ADefinitionThatWasRemoved_StaysBecausePlayersMayHaveUnlockedIt()
    {
        AchievementCatalog.Sync(_host.Factory, Definitions);

        AchievementCatalog.Sync(_host.Factory, [Definitions[0]]);

        Assert.Contains(Rows(), row => row.Id == "hunter");
    }
}
