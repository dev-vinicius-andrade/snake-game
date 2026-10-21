using Library.Commons.Game.Domain.Entities;

namespace Domain.Game.Tests;

public sealed class RoomGameplayTogglesTests
{
    [Fact]
    public void ANewOne_HasNoToggles_AndFollowsTheDefaultForEverything()
    {
        var gameplay = new RoomGameplay();

        Assert.Empty(gameplay.Toggles);
        Assert.Null(gameplay.ToggleOf("perk", "magnet"));
    }

    [Fact]
    public void WithToggle_SwitchesAThingOnOrOff_AndNullGoesBackToTheDefault()
    {
        var gameplay = new RoomGameplay().WithToggle("perk", "magnet", false).WithToggle("event", "maze", true);

        Assert.False(gameplay.IsDefault);
        Assert.Equal(false, gameplay.ToggleOf("perk", "magnet"));
        Assert.Equal(true, gameplay.ToggleOf("event", "maze"));
        Assert.True(gameplay.WithToggle("perk", "magnet", null).WithToggle("event", "maze", null).IsDefault);
    }

    [Fact]
    public void TheWireForm_CarriesTogglesAsOnAndOff_AndReadsThemBack()
    {
        var gameplay = new RoomGameplay { Perks = FeaturePreset.Off }.WithToggle("prize", "gold", false).WithToggle("reward", "big", true);

        var map = gameplay.ToMap();

        Assert.Equal("off", map["prize:gold"]);
        Assert.Equal("on", map["reward:big"]);
        Assert.Equal(gameplay, RoomGameplay.FromMap(map));
    }

    [Fact]
    public void FromMap_IgnoresTogglesItDoesNotUnderstand()
    {
        var gameplay = RoomGameplay.FromMap(new Dictionary<string, string>
        {
            ["PERK:magnet"] = "OFF", ["ghost:x"] = "on", ["perk:"] = "on", [":x"] = "on", ["perk:y"] = "maybe", ["event:zone"] = "on"
        });

        Assert.Equal(new Dictionary<string, bool> { ["perk:magnet"] = false, ["event:zone"] = true }, gameplay.Toggles);
    }

    [Fact]
    public void TheEventbusJson_CarriesTheTogglesToTheGameServer()
    {
        var gameplay = new RoomGameplay { Zone = FeaturePreset.Calm }.WithToggle("perk", "magnet", false).WithToggle("event", "maze", true);

        var back = System.Text.Json.JsonSerializer.Deserialize<RoomGameplay>(System.Text.Json.JsonSerializer.Serialize(gameplay));

        Assert.Equal(gameplay, back);
    }

    [Fact]
    public void TwoRooms_WithTheSameChoices_AreEqual()
    {
        Assert.Equal(new RoomGameplay().WithToggle("perk", "a", true), new RoomGameplay().WithToggle("perk", "a", true));
        Assert.NotEqual(new RoomGameplay().WithToggle("perk", "a", true), new RoomGameplay().WithToggle("perk", "a", false));
    }
}

public sealed class PerkRewardCombineTests
{
    [Fact]
    public void Combine_AddsLivesAndMass_AndJoinsTheFlags()
    {
        var combined = PerkReward.Combine([
            new PerkReward { ExtraLives = 1, BonusMass = 100 },
            new PerkReward { ExtraLives = 2, BonusMass = 50, Mend = true },
            new PerkReward { GrantsRandomPerk = true }]);

        Assert.Equal((3, 150L, true, true), (combined.ExtraLives, combined.BonusMass, combined.Mend, combined.GrantsRandomPerk));
        Assert.Empty(combined.Alongside);
    }

    [Fact]
    public void Combine_LivesStayWithinTheCap()
        => Assert.Equal(PerkReward.MaxLives, PerkReward.Combine([new PerkReward { ExtraLives = 4 }, new PerkReward { ExtraLives = 4 }]).ExtraLives);

    [Fact]
    public void Combine_TwoTierSurges_BothAreGiven()
    {
        var combined = PerkReward.Combine([
            new PerkReward { TierSurgeMin = 1, TierSurgeMax = 2, TierSurgePerk = "magnet" },
            new PerkReward { TierSurgeMin = 3, TierSurgeMax = 3, TierSurgePerk = "armor" }]);

        Assert.Equal((1, 2, "magnet"), (combined.TierSurgeMin, combined.TierSurgeMax, combined.TierSurgePerk));
        var other = Assert.Single(combined.Alongside);
        Assert.Equal((3, 3, "armor"), (other.TierSurgeMin, other.TierSurgeMax, other.TierSurgePerk));
        Assert.False(combined.IsEmpty);
    }

    [Fact]
    public void Combine_NothingOrEmptyRewards_IsEmpty()
    {
        Assert.True(PerkReward.Combine([]).IsEmpty);
        Assert.True(PerkReward.Combine([new PerkReward(), new PerkReward()]).IsEmpty);
    }

    [Fact]
    public void Problem_LooksAtTheRewardsAlongsideToo()
        => Assert.NotNull((new PerkReward { Alongside = [new PerkReward { ExtraLives = 99 }] }).Problem());
}
