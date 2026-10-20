using Application.Manager.Api.Accounts;
using Library.Commons.Game.Domain.Entities;

namespace Application.Manager.Api.Tests;

public sealed class AchievementRulesTests
{
    private static AchievementEntry Valid(string id = "big-eater") => new(id, "game-icons:sparkles", StatKeys.Foods, 100, true,
        new Dictionary<string, AchievementText> { ["en"] = new("Big eater", "Eat 100 fruits."), ["pt"] = new("Comilão", "Coma 100 frutas.") });

    [Fact]
    public void AGoodEntry_HasNoError()
    {
        Assert.Null(AchievementRules.Validate(Valid()));
        Assert.Null(AchievementRules.Validate(Valid() with { Texts = null }));
        Assert.Null(AchievementRules.Validate(Valid() with { Icon = "mdi-trophy" }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("Big Eater")]
    [InlineData("big_eater")]
    [InlineData("-big")]
    [InlineData("big-")]
    [InlineData("big--eater")]
    [InlineData("a-very-long-achievement-identifier-that-goes-on")]
    public void AnIdThatIsNotALowerCaseSlug_IsRefused(string id)
        => Assert.Equal(AchievementErrors.InvalidId, AchievementRules.Validate(Valid(id)));

    [Theory]
    [InlineData("")]
    [InlineData("snake")]
    [InlineData("Game-Icons:Snake")]
    [InlineData("game icons:snake")]
    [InlineData("mdi-")]
    [InlineData("mdi_trophy")]
    public void AnIconThatIsNotNamedLikeAGameIcon_IsRefused(string icon)
        => Assert.Equal(AchievementErrors.InvalidIcon, AchievementRules.Validate(Valid() with { Icon = icon }));

    [Theory]
    [InlineData("nothing")]
    [InlineData("")]
    [InlineData("Games")]
    public void AStatThatTheGameDoesNotKeep_IsRefused(string stat)
        => Assert.Equal(AchievementErrors.InvalidStat, AchievementRules.Validate(Valid() with { Stat = stat }));

    [Fact]
    public void EveryStatTheGameKeeps_IsAccepted()
    {
        foreach (var stat in StatKeys.Counters.Concat(StatKeys.Maxima))
            Assert.Null(AchievementRules.Validate(Valid() with { Stat = stat }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(1_000_000_001)]
    public void AThresholdOutsideOneToABillion_IsRefused(long threshold)
        => Assert.Equal(AchievementErrors.InvalidThreshold, AchievementRules.Validate(Valid() with { Threshold = threshold }));

    [Fact]
    public void Texts_OnlyForTheLanguagesTheSiteHas_WithANameAndAShortDescription()
    {
        Assert.Equal(AchievementErrors.InvalidText, AchievementRules.Validate(Valid() with { Texts = new Dictionary<string, AchievementText> { ["fr"] = new("Gros", "x") } }));
        Assert.Equal(AchievementErrors.InvalidText, AchievementRules.Validate(Valid() with { Texts = new Dictionary<string, AchievementText> { ["en"] = new(" ", "x") } }));
        Assert.Equal(AchievementErrors.InvalidText, AchievementRules.Validate(Valid() with { Texts = new Dictionary<string, AchievementText> { ["en"] = new(new string('n', 41), "x") } }));
        Assert.Equal(AchievementErrors.InvalidText, AchievementRules.Validate(Valid() with { Texts = new Dictionary<string, AchievementText> { ["en"] = new("Name", new string('d', 201)) } }));
        Assert.Null(AchievementRules.Validate(Valid() with { Texts = new Dictionary<string, AchievementText> { ["en"] = new("Name", "") } }));
    }

    [Fact]
    public void TheCodeCatalog_HasTheDefaults_AndCannotBeChanged()
    {
        var catalog = new CodeAchievementCatalog();

        Assert.Equal(Achievements.Defaults.Select(definition => definition.Id), catalog.All().Select(entry => entry.Id));
        Assert.All(catalog.All(), entry => Assert.True(entry.Enabled));
        Assert.False(catalog.IsWritable);
        Assert.Equal(AchievementChange.ReadOnly, catalog.Save(Valid()));
        Assert.Equal(AchievementChange.ReadOnly, catalog.Delete("hunter"));
    }
}
