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
        Assert.Equal(GameplayConfig.Defaults.Prize, back.Prize);
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
}
