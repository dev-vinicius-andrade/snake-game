using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class GameplayConfigTests
{
    [Fact]
    public void Defaults_HaveTheBuiltInPerksListedOutAndAValidPrize()
    {
        var defaults = GameplayConfig.Defaults;

        Assert.Equal(PerkOptions.BuiltIn.Select(perk => perk.Id), defaults.Perks.Perks.Select(perk => perk.Id));
        defaults.EnsureValid();
    }

    [Fact]
    public void Json_WritesEffectsAsNamesAndReadsThemBack()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(GameplayConfig.Defaults, GameplayConfig.Json);

        Assert.Matches("\"effect\": \"Magnet\"", json);
        var back = System.Text.Json.JsonSerializer.Deserialize<GameplayConfig>(json, GameplayConfig.Json)!;
        Assert.Equal(GameplayConfig.Defaults.Perks.Perks.Count, back.Perks.Perks.Count);
        Assert.Equal(PerkEffect.Magnet, back.Perks.Perks.First(perk => perk.Id == "magnet").Effects.Single().Effect);
        var expected = GameplayConfig.Defaults.Prize.Prizes.Single();
        var actual = back.Prize.Prizes.Single();
        Assert.Equal((expected.Id, expected.Value, expected.SpawnIntervalSeconds, expected.Color), (actual.Id, actual.Value, actual.SpawnIntervalSeconds, actual.Color));
    }

    [Fact]
    public void EnsureValid_RefusesAPerkThatCannotWork()
    {
        var config = new GameplayConfig
        {
            Perks = new PerkOptions { Perks = [new() { Id = "x", Name = "X", FoodsRequired = 0, Effect = PerkEffect.Magnet, Amount = 1 }] }
        };

        Assert.Throws<ArgumentOutOfRangeException>(config.EnsureValid);
    }

    [Fact]
    public void Defaults_HaveLightningOnAndValid()
    {
        var lightning = GameplayConfig.Defaults.WorldEvents;

        Assert.True(lightning.Enabled);
        Assert.True(lightning.Lightning.Enabled);
    }

    [Fact]
    public void Json_WorldEventsAreWrittenAndReadBack()
    {
        var config = GameplayConfig.Defaults with
        {
            WorldEvents = new WorldEventOptions { Lightning = new LightningOptions { Radius = 123, ExcludedRooms = ["quiet"] } }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(config, GameplayConfig.Json);
        var back = System.Text.Json.JsonSerializer.Deserialize<GameplayConfig>(json, GameplayConfig.Json)!;

        Assert.Contains("\"worldEvents\"", json);
        Assert.Equal(123, back.WorldEvents.Lightning.Radius);
        Assert.Equal(["quiet"], back.WorldEvents.Lightning.ExcludedRooms);
    }

    [Fact]
    public void EnsureValid_RefusesLightningThatCannotWork()
    {
        var config = new GameplayConfig { WorldEvents = new WorldEventOptions { Lightning = new LightningOptions { Radius = 0 } } };

        Assert.Throws<ArgumentOutOfRangeException>(config.EnsureValid);
    }

    [Fact]
    public void EnsureValid_RefusesAPrizeThatCannotWork()
    {
        var config = new GameplayConfig { Prize = new PrizeOptions { SpawnIntervalSeconds = 0 } };

        Assert.Throws<ArgumentOutOfRangeException>(config.EnsureValid);
    }

    [Fact]
    public void WithBuiltInPerks_FillsAnEmptyListAndLeavesAFullOne()
    {
        var empty = new GameplayConfig().WithBuiltInPerks();
        var custom = new GameplayConfig { Perks = new PerkOptions { Perks = [new() { Id = "a", Name = "A", FoodsRequired = 1, Effect = PerkEffect.Magnet, Amount = 5 }] } };

        Assert.Equal(PerkOptions.BuiltIn.Count, empty.Perks.Perks.Count);
        Assert.Single(custom.WithBuiltInPerks().Perks.Perks);
    }

    [Fact]
    public void Normalized_MovesTheSingleOldEffectIntoTheList()
    {
        var config = new GameplayConfig
        {
            Perks = new PerkOptions { Perks = [new() { Id = "a", Name = "A", FoodsRequired = 1, Effect = PerkEffect.Magnet, Amount = 60 }] }
        };

        var perk = config.Normalized().Perks.Perks.Single();

        Assert.Null(perk.Effect);
        Assert.Equal(0, perk.Amount);
        var entry = Assert.Single(perk.Effects);
        Assert.Equal((PerkEffect.Magnet, 60.0), (entry.Effect, entry.Amount));
    }

    [Fact]
    public void Defaults_ListEveryEffectInTheListAndNotInTheOldFields()
    {
        var perks = GameplayConfig.Defaults.Perks.Perks;

        Assert.All(perks, perk =>
        {
            Assert.Null(perk.Effect);
            Assert.NotEmpty(perk.Effects);
        });
    }

    [Fact]
    public void Normalized_LeavesRoomsAndScriptsAlone()
    {
        var config = new GameplayConfig
        {
            Perks = new PerkOptions { Perks = [new() { Id = "a", Name = "A", FoodsRequired = 1, Effects = [new() { Effect = PerkEffect.Magnet, Amount = 1 }], Rooms = ["R1"], Script = "x" }] }
        };

        var perk = config.Normalized().Perks.Perks.Single();

        Assert.Equal(new[] { "R1" }, perk.Rooms);
        Assert.Equal("x", perk.Script);
    }

    [Fact]
    public void Normalized_KeepsWhetherAPerkIsOn()
    {
        var config = new GameplayConfig
        {
            Perks = new PerkOptions { Perks = [new() { Id = "a", Name = "A", FoodsRequired = 1, Effect = PerkEffect.Magnet, Amount = 1, Enabled = false }] }
        };

        Assert.False(config.Normalized().Perks.Perks.Single().Enabled);
    }

    [Fact]
    public void Normalized_TheOldSinglePrizeBecomesAKind()
    {
        var config = new GameplayConfig { Prize = new PrizeOptions { Value = 55, SpawnIntervalSeconds = 33 } };

        var kind = Assert.Single(config.Normalized().Prize.Prizes);

        Assert.Equal("prize", kind.Id);
        Assert.Equal(55, kind.Value);
        Assert.Equal(33, kind.SpawnIntervalSeconds);
    }

    [Fact]
    public void Normalized_AListOfKindsIsKeptAsItIs()
    {
        var config = new GameplayConfig { Prize = new PrizeOptions { Prizes = [new() { Id = "a", Name = "A" }, new() { Id = "b", Name = "B" }] } };

        Assert.Equal(new[] { "a", "b" }, config.Normalized().Prize.Prizes.Select(kind => kind.Id));
    }

    [Fact]
    public void Defaults_HaveOnePrizeKindWrittenOut()
    {
        Assert.Single(GameplayConfig.Defaults.Prize.Prizes);
    }

    [Fact]
    public void EnsureValid_AKindThatGrantsAPerkNeedsThatPerkToExist()
    {
        var config = new GameplayConfig
        {
            Perks = new PerkOptions { Perks = [new() { Id = "real", Name = "Real", FoodsRequired = 5, Effect = PerkEffect.Magnet, Amount = 1 }] },
            Prize = new PrizeOptions { Prizes = [new() { Id = "gift", Name = "Gift", GrantsPerk = "ghost" }] }
        };

        var failure = Assert.Throws<ArgumentException>(config.EnsureValid);

        Assert.Contains("gift", failure.Message);
        Assert.Contains("ghost", failure.Message);
    }

    [Fact]
    public void EnsureValid_AKindThatGrantsAnExistingPerkIsFine()
    {
        var config = new GameplayConfig
        {
            Perks = new PerkOptions { Perks = [new() { Id = "real", Name = "Real", FoodsRequired = 5, Effect = PerkEffect.Magnet, Amount = 1 }] },
            Prize = new PrizeOptions { Prizes = [new() { Id = "gift", Name = "Gift", GrantsPerk = "real" }] }
        };

        config.EnsureValid();
    }

    [Fact]
    public void EnsureValid_AKindCanGrantAPerkOfTheBuiltInSetWhenNoListWasGiven()
    {
        var config = new GameplayConfig { Prize = new PrizeOptions { Prizes = [new() { Id = "gift", Name = "Gift", GrantsPerk = "magnet" }] } };

        config.EnsureValid();
    }

    [Fact]
    public void Json_WritesAndReadsTheKinds()
    {
        var config = new GameplayConfig { Prize = new PrizeOptions { Prizes = [new() { Id = "gift", Name = "Gift", Color = "#ff00aa", Value = 3, Rooms = ["R1"] }] } };

        var back = System.Text.Json.JsonSerializer.Deserialize<GameplayConfig>(System.Text.Json.JsonSerializer.Serialize(config, GameplayConfig.Json), GameplayConfig.Json)!;

        var kind = Assert.Single(back.Prize.Prizes);
        Assert.Equal((("gift", "#ff00aa", 3L)), (kind.Id, kind.Color, kind.Value));
        Assert.Equal(new[] { "R1" }, kind.Rooms);
    }
}
