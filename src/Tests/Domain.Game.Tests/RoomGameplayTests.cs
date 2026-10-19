using Library.Commons.Game.Domain.Entities;

namespace Domain.Game.Tests;

public sealed class RoomGameplayTests
{
    [Fact]
    public void ANewOne_InheritsEverything()
    {
        var gameplay = new RoomGameplay();

        Assert.True(gameplay.IsDefault);
        Assert.All(RoomGameplay.Features, feature => Assert.Equal(FeaturePreset.Inherit, gameplay.PresetOf(feature)));
    }

    [Fact]
    public void ChangingOneFeature_IsNoLongerTheDefault()
    {
        var gameplay = new RoomGameplay { Turret = FeaturePreset.Off };

        Assert.False(gameplay.IsDefault);
        Assert.Equal(FeaturePreset.Off, gameplay.PresetOf("turret"));
        Assert.Equal(FeaturePreset.Inherit, gameplay.PresetOf("zone"));
    }

    [Fact]
    public void ToMap_NamesEveryFeatureWithItsPreset_InLowerCase()
    {
        var map = new RoomGameplay { Perks = FeaturePreset.Off, Lightning = FeaturePreset.Wild, Zone = FeaturePreset.Calm }.ToMap();

        Assert.Equal(
            new Dictionary<string, string>
            {
                ["perks"] = "off", ["prizes"] = "inherit", ["lightning"] = "wild", ["meteors"] = "inherit", ["turret"] = "inherit", ["zone"] = "calm"
            },
            map);
    }

    [Fact]
    public void FromMap_ReadsWhatToMapWrote()
    {
        var gameplay = new RoomGameplay { Prizes = FeaturePreset.Off, Meteors = FeaturePreset.Normal, Turret = FeaturePreset.Wild };

        Assert.Equal(gameplay, RoomGameplay.FromMap(gameplay.ToMap()));
    }

    [Fact]
    public void FromMap_IsKindToWhatItDoesNotUnderstand()
    {
        var gameplay = RoomGameplay.FromMap(new Dictionary<string, string> { ["LIGHTNING"] = "Wild", ["zone"] = "maybe", ["nothing"] = "off", ["turret"] = "" });

        Assert.Equal(new RoomGameplay { Lightning = FeaturePreset.Wild }, gameplay);
    }

    [Fact]
    public void FromMap_NothingAtAll_IsTheDefault()
    {
        Assert.True(RoomGameplay.FromMap(null).IsDefault);
        Assert.True(RoomGameplay.FromMap(new Dictionary<string, string>()).IsDefault);
    }

    [Fact]
    public void OnlyOffAndOnApplyToPerksAndPrizes_SoWildOrCalmThereMeanOn()
    {
        Assert.False(new RoomGameplay { Perks = FeaturePreset.Wild }.PerksOff);
        Assert.True(new RoomGameplay { Perks = FeaturePreset.Off }.PerksOff);
        Assert.True(new RoomGameplay { Prizes = FeaturePreset.Off }.PrizesOff);
        Assert.False(new RoomGameplay().PrizesOff);
    }
}
