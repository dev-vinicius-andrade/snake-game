using Library.Commons.Game.Domain.Entities;
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
    public void Defaults_HaveLightningStormsAndMeteorRainOn()
    {
        var events = GameplayConfig.Defaults.WorldEvents;

        Assert.True(events.Enabled);
        Assert.True(events.Lightning.Enabled);
        Assert.Equal((5, 12, 6), (events.Lightning.MinStrikes, events.Lightning.MaxStrikes, events.Lightning.StormSeconds));
        Assert.True(events.Meteors.Enabled);
        Assert.True(events.Meteors.Radius > events.Lightning.Radius);
        Assert.True(events.Meteors.TelegraphMilliseconds > events.Lightning.TelegraphMilliseconds);
    }

    [Fact]
    public void Json_AnOlderConfigWithASingleStrikeLightningStillLoads_AsAStormOfOne()
    {
        const string old = "{\"worldEvents\":{\"enabled\":true,\"lightning\":{\"enabled\":true,\"minIntervalSeconds\":30,\"maxIntervalSeconds\":90,\"radius\":90,\"telegraphMilliseconds\":1500}}}";

        var config = System.Text.Json.JsonSerializer.Deserialize<GameplayConfig>(old, GameplayConfig.Json)!;

        config.EnsureValid();
        IStormSettings lightning = config.WorldEvents.Lightning;
        Assert.Equal((1, 1), (lightning.MinCount, lightning.MaxCount));
        Assert.True(config.WorldEvents.Meteors.Enabled);
    }

    [Fact]
    public void Defaults_HaveATurretOn()
    {
        var turret = GameplayConfig.Defaults.WorldEvents.Turret;

        Assert.True(turret.Enabled);
        Assert.True(turret.HitPoints > 1);
        Assert.Equal(1, turret.MinPlayers);
    }

    [Fact]
    public void Json_TurretSettingsAreWrittenAndReadBack()
    {
        var config = GameplayConfig.Defaults with
        {
            WorldEvents = new WorldEventOptions
            {
                Turret = new TurretOptions { HitPoints = 77, Arms = 3, SpinDegrees = -20, ShotSpeed = 0.8, RewardMass = 5, GrantsPerk = "magnet", Rooms = ["arena"] }
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(config, GameplayConfig.Json);
        var back = System.Text.Json.JsonSerializer.Deserialize<GameplayConfig>(json, GameplayConfig.Json)!;

        Assert.Contains("\"turret\"", json);
        Assert.Equal((77, 3, -20.0, 0.8, 5L, "magnet"), (back.WorldEvents.Turret.HitPoints, back.WorldEvents.Turret.Arms, back.WorldEvents.Turret.SpinDegrees, back.WorldEvents.Turret.ShotSpeed, back.WorldEvents.Turret.RewardMass, back.WorldEvents.Turret.GrantsPerk));
        Assert.Equal(["arena"], back.WorldEvents.Turret.Rooms);
    }

    [Fact]
    public void Json_AConfigWithoutATurretStillLoadsAndGetsTheDefaultOne()
    {
        const string old = "{\"worldEvents\":{\"enabled\":true}}";

        var config = System.Text.Json.JsonSerializer.Deserialize<GameplayConfig>(old, GameplayConfig.Json)!;

        config.EnsureValid();
        Assert.True(config.WorldEvents.Turret.Enabled);
    }

    [Fact]
    public void Json_PerkTiersAreWrittenAndReadBack()
    {
        var config = GameplayConfig.Defaults with
        {
            Perks = new PerkOptions
            {
                KillFoodsCredit = 5,
                Perks = [new PerkDefinition { Id = "m", Name = "M", FoodsRequired = 3, Effect = PerkEffect.Magnet, Amount = 10, Tiers = [new PerkTier { FoodsRequired = 9, Multiplier = 1.5 }] }]
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(config, GameplayConfig.Json);
        var back = System.Text.Json.JsonSerializer.Deserialize<GameplayConfig>(json, GameplayConfig.Json)!;

        Assert.Contains("\"tiers\"", json);
        Assert.Equal(5, back.Perks.KillFoodsCredit);
        Assert.Equal((9, 1.5), (back.Perks.Perks[0].Tiers[0].FoodsRequired, back.Perks.Perks[0].Tiers[0].Multiplier));
        back.EnsureValid();
    }

    [Fact]
    public void Defaults_TheBuiltInPerksHaveTiers_AndStayValid()
    {
        var defaults = GameplayConfig.Defaults;

        Assert.Contains(defaults.Perks.Perks, perk => perk.Tiers.Count > 0);
        defaults.EnsureValid();
    }

    [Fact]
    public void EnsureValid_RefusesAPerkTierThatCannotWork()
    {
        var config = new GameplayConfig
        {
            Perks = new PerkOptions
            {
                Perks = [new PerkDefinition { Id = "m", Name = "M", FoodsRequired = 3, Effect = PerkEffect.Magnet, Amount = 10, Tiers = [new PerkTier { FoodsRequired = 2, Multiplier = 1.5 }] }]
            }
        };

        Assert.Throws<ArgumentOutOfRangeException>(config.EnsureValid);
    }

    [Fact]
    public void Defaults_HaveAShrinkingZoneOn()
    {
        var zone = GameplayConfig.Defaults.WorldEvents.Zone;

        Assert.True(zone.Enabled);
        Assert.True(zone.ShrinkSeconds >= 5);
        Assert.True(zone.MinPlayers >= 2);
    }

    [Fact]
    public void Json_ZoneSettingsAreWrittenAndReadBack()
    {
        var config = GameplayConfig.Defaults with
        {
            WorldEvents = new WorldEventOptions
            {
                Zone = new ZoneOptions { WarningSeconds = 7, ShrinkSeconds = 50, HoldSeconds = 0, FinalRadius = 90, DamageMass = 9, ExcludedRooms = ["calm"] }
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(config, GameplayConfig.Json);
        var back = System.Text.Json.JsonSerializer.Deserialize<GameplayConfig>(json, GameplayConfig.Json)!;

        Assert.Contains("\"zone\"", json);
        var zone = back.WorldEvents.Zone;
        Assert.Equal((7, 50, 0, 90, 9), (zone.WarningSeconds, zone.ShrinkSeconds, zone.HoldSeconds, zone.FinalRadius, zone.DamageMass));
        Assert.Equal(["calm"], zone.ExcludedRooms);
    }

    [Fact]
    public void Json_AConfigWithoutAZoneStillLoadsAndGetsTheDefaultOne()
    {
        const string old = "{\"worldEvents\":{\"enabled\":true}}";

        var config = System.Text.Json.JsonSerializer.Deserialize<GameplayConfig>(old, GameplayConfig.Json)!;

        config.EnsureValid();
        Assert.True(config.WorldEvents.Zone.Enabled);
    }

    [Fact]
    public void EnsureValid_RefusesAZoneThatCannotWork()
    {
        var config = new GameplayConfig { WorldEvents = new WorldEventOptions { Zone = new ZoneOptions { ShrinkSeconds = 1 } } };

        Assert.Throws<ArgumentOutOfRangeException>(config.EnsureValid);
    }

    [Fact]
    public void EnsureValid_RefusesATurretThatCannotWork()
    {
        var config = new GameplayConfig { WorldEvents = new WorldEventOptions { Turret = new TurretOptions { Arms = 0 } } };

        Assert.Throws<ArgumentOutOfRangeException>(config.EnsureValid);
    }

    [Fact]
    public void EnsureValid_ATurretCanOnlyGiveAPerkThatExists()
    {
        var config = GameplayConfig.Defaults with { WorldEvents = new WorldEventOptions { Turret = new TurretOptions { GrantsPerk = "nope" } } };

        Assert.Throws<ArgumentException>(config.EnsureValid);
    }

    [Fact]
    public void EnsureValid_ATurretMayGiveABuiltInPerk()
    {
        var config = GameplayConfig.Defaults with { WorldEvents = new WorldEventOptions { Turret = new TurretOptions { GrantsPerk = "magnet" } } };

        config.EnsureValid();
    }

    [Fact]
    public void EnsureValid_RefusesMeteorsThatCannotWork()
    {
        var config = new GameplayConfig { WorldEvents = new WorldEventOptions { Meteors = new MeteorOptions { RainSeconds = 0 } } };

        Assert.Throws<ArgumentOutOfRangeException>(config.EnsureValid);
    }

    [Fact]
    public void Json_WorldEventsAreWrittenAndReadBack()
    {
        var config = GameplayConfig.Defaults with
        {
            WorldEvents = new WorldEventOptions
            {
                Lightning = new LightningOptions { Radius = 123, MinStrikes = 2, MaxStrikes = 3, StormSeconds = 4, AimedShare = 0.5, ExcludedRooms = ["quiet"] },
                Meteors = new MeteorOptions { MinMeteors = 3, MaxMeteors = 7, RainSeconds = 5, Rooms = ["arena"] }
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(config, GameplayConfig.Json);
        var back = System.Text.Json.JsonSerializer.Deserialize<GameplayConfig>(json, GameplayConfig.Json)!;

        Assert.Contains("\"worldEvents\"", json);
        Assert.Equal(123, back.WorldEvents.Lightning.Radius);
        Assert.Equal(["quiet"], back.WorldEvents.Lightning.ExcludedRooms);
        Assert.Equal((2, 3, 4, 0.5), (back.WorldEvents.Lightning.MinStrikes, back.WorldEvents.Lightning.MaxStrikes, back.WorldEvents.Lightning.StormSeconds, back.WorldEvents.Lightning.AimedShare));
        Assert.Equal((3, 7, 5), (back.WorldEvents.Meteors.MinMeteors, back.WorldEvents.Meteors.MaxMeteors, back.WorldEvents.Meteors.RainSeconds));
        Assert.Equal(["arena"], back.WorldEvents.Meteors.Rooms);
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
    public void EnsureValid_ARewardThatLiftsAPerkNeedsThatPerkToExist()
    {
        var config = new GameplayConfig
        {
            Perks = new PerkOptions { Perks = [new() { Id = "real", Name = "Real", FoodsRequired = 5, Effect = PerkEffect.Magnet, Amount = 1 }] },
            Prize = new PrizeOptions { Prizes = [new() { Id = "gift", Name = "Gift", Reward = new PerkReward { TierSurgeMin = 1, TierSurgeMax = 1, TierSurgePerk = "ghost" } }] }
        };

        var failure = Assert.Throws<ArgumentException>(config.EnsureValid);

        Assert.Contains("ghost", failure.Message);
    }

    [Theory]
    [InlineData(6, 0, 0, 0)]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(0, 11, 0, 0)]
    [InlineData(0, 0, 11, 0)]
    [InlineData(0, 0, 0, 10001)]
    public void EnsureValid_ARewardOutOfRange_IsRefused_WhereverItIsSet(int lives, int surgeMin, int surgeMax, long mass)
    {
        var reward = new PerkReward { ExtraLives = lives, TierSurgeMin = surgeMin, TierSurgeMax = surgeMax, BonusMass = mass };
        var base_ = GameplayConfig.Defaults;

        Assert.Throws<ArgumentException>((base_ with { Prize = new PrizeOptions { Prizes = [new() { Id = "gift", Name = "Gift", Reward = reward }] } }).EnsureValid);
        Assert.Throws<ArgumentException>((base_ with { WorldEvents = base_.WorldEvents with { Turret = new TurretOptions { Reward = reward } } }).EnsureValid);
        Assert.Throws<ArgumentException>((base_ with { WorldEvents = base_.WorldEvents with { Bounty = new BountyOptions { Reward = reward } } }).EnsureValid);
        Assert.Throws<ArgumentException>((base_ with { WorldEvents = base_.WorldEvents with { Maze = new MazeOptions { PrizeReward = reward } } }).EnsureValid);
    }

    [Fact]
    public void Json_RewardsAndTheNewPerkFieldsRoundTrip_AndOldJsonStillReads()
    {
        var config = GameplayConfig.Defaults with
        {
            Prize = new PrizeOptions { Prizes = [new() { Id = "gift", Name = "Gift", Reward = new PerkReward { ExtraLives = 1, TierSurgeMin = 1, TierSurgeMax = 3, TierSurgePerk = "magnet", Mend = true }}] },
            Perks = GameplayConfig.Defaults.Perks with { ExtraLifeKeepShare = 0.25, ExtraLifeProtectSeconds = 5 }
        };

        var back = System.Text.Json.JsonSerializer.Deserialize<GameplayConfig>(System.Text.Json.JsonSerializer.Serialize(config, GameplayConfig.Json), GameplayConfig.Json)!;

        Assert.Equal(config.Prize.Prizes[0].Reward, back.Prize.Prizes[0].Reward);
        Assert.Equal((0.25, 5), (back.Perks.ExtraLifeKeepShare, back.Perks.ExtraLifeProtectSeconds));
        var old = System.Text.Json.JsonSerializer.Deserialize<GameplayConfig>("{\"prize\":{\"prizes\":[{\"id\":\"x\",\"name\":\"X\",\"grantsPerk\":\"magnet\"}]}}", GameplayConfig.Json)!;
        Assert.True(old.Prize.Prizes[0].Reward.IsEmpty);
        old.EnsureValid();
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
