using Application.Manager.Api.Accounts;
using Application.Manager.Api.Persistence;
using Library.Commons.Game.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;

namespace Application.Manager.Api.Tests.Persistence;

public sealed class EfAchievementCatalogTests : IDisposable
{
    private readonly PersistenceTestHost _host = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero));

    public void Dispose() => _host.Dispose();

    private EfAchievementCatalog Catalog() => new(_host.Factory, _time);

    private static AchievementEntry Entry(string id = "big-eater", long threshold = 100) => new(id, "game-icons:sparkles", StatKeys.Foods, threshold, true,
        new Dictionary<string, AchievementText> { ["en"] = new("Big eater", "Eat 100 fruits."), ["pt"] = new("Comilão", "Coma 100 frutas.") });

    [Fact]
    public void AnEmptyCatalog_IsFilledFromTheDefaults_WhenSynced_AndIsWritable()
    {
        AchievementCatalog.Sync(_host.Factory, Achievements.Defaults);

        var catalog = Catalog();

        Assert.True(catalog.IsWritable);
        Assert.Equal(Achievements.Defaults.Select(definition => definition.Id), catalog.All().Select(entry => entry.Id));
    }

    [Fact]
    public void Save_ANewAchievement_IsAddedAtTheEnd_WithItsTexts()
    {
        var catalog = Catalog();
        AchievementCatalog.Sync(_host.Factory, Achievements.Defaults);

        Assert.Equal(AchievementChange.Saved, catalog.Save(Entry()));

        var saved = Catalog().All().Last();
        Assert.Equal("big-eater", saved.Id);
        Assert.Equal(("game-icons:sparkles", StatKeys.Foods, 100L, true), (saved.Icon, saved.Stat, saved.Threshold, saved.Enabled));
        Assert.Equal("Comilão", saved.Texts!["pt"].Name);
        Assert.Equal("Eat 100 fruits.", saved.Texts["en"].Description);
    }

    [Fact]
    public void Save_AnExistingOne_IsChangedInPlace_KeepsItsPlace_AndTextsAreReplaced()
    {
        var catalog = Catalog();
        catalog.Save(Entry("a-one"));
        catalog.Save(Entry("b-two"));
        catalog.Save(Entry("c-three"));

        catalog.Save(Entry("b-two", threshold: 7) with { Enabled = false, Texts = new Dictionary<string, AchievementText> { ["en"] = new("Two", "") } });

        var all = Catalog().All();
        Assert.Equal(new[] { "a-one", "b-two", "c-three" }, all.Select(entry => entry.Id));
        var two = all[1];
        Assert.Equal((7L, false), (two.Threshold, two.Enabled));
        Assert.Equal(new[] { "en" }, two.Texts!.Keys);
    }

    [Fact]
    public void Definitions_AreOnlyTheEnabledOnes_InOrder()
    {
        var catalog = Catalog();
        catalog.Save(Entry("a-one"));
        catalog.Save(Entry("b-two") with { Enabled = false });
        catalog.Save(Entry("c-three"));

        Assert.Equal(new[] { "a-one", "c-three" }, catalog.Definitions().Select(definition => definition.Id));
    }

    [Fact]
    public void Delete_AnAchievementNobodyUnlocked_IsRemovedWithItsTexts()
    {
        var catalog = Catalog();
        catalog.Save(Entry());

        Assert.Equal(AchievementChange.Deleted, catalog.Delete("big-eater"));

        Assert.Empty(Catalog().All());
        using var context = _host.Factory.CreateDbContext();
        Assert.Empty(context.AchievementTexts);
    }

    [Fact]
    public void Delete_AnAchievementSomeoneUnlocked_IsRefused_ItCanBeSwitchedOffInstead()
    {
        var catalog = Catalog();
        var definitions = new[] { new AchievementDefinition("big-eater", "game-icons:sparkles", StatKeys.Foods, 1) };
        catalog.Save(Entry() with { Threshold = 1 });
        _host.Stats().Apply(Guid.NewGuid(), Guid.NewGuid(), new Dictionary<string, long> { [StatKeys.Foods] = 5 }, new Dictionary<string, long>(), _time.GetUtcNow(), definitions);

        Assert.Equal(AchievementChange.InUse, catalog.Delete("big-eater"));
        Assert.Equal(AchievementChange.Saved, catalog.Save(Entry() with { Enabled = false }));
    }

    [Fact]
    public void Delete_SomethingThatIsNotThere_IsNotFound()
        => Assert.Equal(AchievementChange.NotFound, Catalog().Delete("nothing-here"));

    [Fact]
    public void Another_CatalogSeesAChangeAfterTheRefreshTime_AndNotBefore()
    {
        var first = Catalog();
        var second = Catalog();
        Assert.Empty(second.All());

        first.Save(Entry());

        Assert.Empty(second.All());
        _time.Advance(EfAchievementCatalog.RefreshEvery + TimeSpan.FromSeconds(1));
        Assert.Single(second.All());
    }

    [Fact]
    public void TheCatalogThatSavedSeesItAtOnce()
    {
        var catalog = Catalog();
        Assert.Empty(catalog.All());

        catalog.Save(Entry());

        Assert.Single(catalog.All());
    }
}
