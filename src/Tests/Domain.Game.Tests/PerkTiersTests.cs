using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class PerkTiersTests
{
    private static PerkDefinition Magnet(params PerkTier[] tiers) => new()
    {
        Id = "magnet", Name = "Magnet", FoodsRequired = 6, Effect = PerkEffect.Magnet, Amount = 60, Tiers = [.. tiers]
    };

    private static PerkRules Rules(params PerkDefinition[] perks) => new(new PerkOptions { Perks = [.. perks] });

    private static readonly PerkTier Second = new() { FoodsRequired = 12, Multiplier = 1.5 };
    private static readonly PerkTier Third = new() { FoodsRequired = 24, Multiplier = 2 };

    [Fact]
    public void EffectsFor_APerkWithoutTiers_IsAsItAlwaysWas()
    {
        var rules = Rules(Magnet());

        Assert.Equal(60, rules.EffectsFor(6).MagnetRadius);
        Assert.Equal(60, rules.EffectsFor(500).MagnetRadius);
    }

    [Theory]
    [InlineData(5, 0)]
    [InlineData(6, 60)]
    [InlineData(11, 60)]
    [InlineData(12, 90)]
    [InlineData(23, 90)]
    [InlineData(24, 120)]
    [InlineData(500, 120)]
    public void EffectsFor_EachTierScalesWhatThePerkDoes(int foods, double radius)
    {
        var rules = Rules(Magnet(Second, Third));

        Assert.Equal(radius, rules.EffectsFor(foods).MagnetRadius);
    }

    [Fact]
    public void TiersFor_SaysWhichTierEachPerkIsAt_InTheOrderOfTheUnlockedIds()
    {
        var rules = Rules(
            Magnet(Second, Third),
            new PerkDefinition { Id = "swift", Name = "Swift", FoodsRequired = 8, Effect = PerkEffect.BoostSpeed, Amount = 0.25 });

        Assert.Equal(["magnet", "swift"], rules.UnlockedIds(13));
        Assert.Equal([2, 1], rules.TiersFor(13));
        Assert.Equal([3, 1], rules.TiersFor(40));
        Assert.Equal([1], rules.TiersFor(7));
        Assert.Empty(rules.TiersFor(2));
    }

    [Fact]
    public void TiersFor_APerkGivenToASnakeWithFewFoods_StartsAtTheFirstTier()
    {
        var rules = Rules(Magnet(Second));

        Assert.Equal([1], rules.TiersFor(0, null, ["magnet"]));
        Assert.Equal(60, rules.EffectsFor(0, null, ["magnet"]).MagnetRadius);
    }

    [Fact]
    public void EffectsFor_ShotsAreWholeNumbers_SoATierRoundsThem()
    {
        var twin = new PerkDefinition { Id = "twin", Name = "Twin", FoodsRequired = 1, Effect = PerkEffect.ExtraShots, Amount = 1, Tiers = [new PerkTier { FoodsRequired = 5, Multiplier = 2 }] };
        var rules = Rules(twin);

        Assert.Equal(1, rules.EffectsFor(1).ExtraShots);
        Assert.Equal(2, rules.EffectsFor(5).ExtraShots);
    }

    [Fact]
    public void EffectsFor_ARatioNeverGoesPastWhatTheEffectCanTake()
    {
        var armor = new PerkDefinition { Id = "armor", Name = "Armor", FoodsRequired = 1, Effect = PerkEffect.Armor, Amount = 0.6, Tiers = [new PerkTier { FoodsRequired = 5, Multiplier = 4 }] };
        var rules = Rules(armor);

        var damageFactor = rules.EffectsFor(5).DamageFactor;

        Assert.InRange(damageFactor, 0.2, 0.2 + 1e-9);
    }

    [Fact]
    public void EffectsFor_ASwitchHasNoStrength_SoTiersDoNotChangeIt()
    {
        var pierce = new PerkDefinition { Id = "pierce", Name = "Pierce", FoodsRequired = 1, Effect = PerkEffect.PiercingShot, Amount = 1, Tiers = [Second] };
        var rules = Rules(pierce);

        Assert.True(rules.EffectsFor(1).Piercing);
        Assert.True(rules.EffectsFor(500).Piercing);
    }

    [Fact]
    public void EffectsFor_TiersApplyToEveryEffectOfAPerk()
    {
        var both = new PerkDefinition
        {
            Id = "both", Name = "Both", FoodsRequired = 1, Tiers = [new PerkTier { FoodsRequired = 5, Multiplier = 2 }],
            Effects = [new PerkEffectEntry { Effect = PerkEffect.Magnet, Amount = 50 }, new PerkEffectEntry { Effect = PerkEffect.Regeneration, Amount = 3 }]
        };

        var effects = Rules(both).EffectsFor(5);

        Assert.Equal((100.0, 6.0), (effects.MagnetRadius, effects.RegenPerSecond));
    }

    [Fact]
    public void PerkDefinition_TiersListThePerkWithItsFirstTierFirst()
    {
        var perk = Magnet(Third, Second);

        Assert.Equal([1.0, 1.5, 2.0], perk.TiersInOrder().Select(tier => tier.Multiplier));
        Assert.Equal([6, 12, 24], perk.TiersInOrder().Select(tier => tier.FoodsRequired));
    }

    [Theory]
    [InlineData(6, 1.5)]
    [InlineData(5, 1.5)]
    [InlineData(12, 0)]
    [InlineData(12, -1)]
    [InlineData(12, 11)]
    public void Constructor_ATierThatCannotWork_IsRefused(int foods, double multiplier)
    {
        var tier = new PerkTier { FoodsRequired = foods, Multiplier = multiplier };

        Assert.Throws<ArgumentOutOfRangeException>(() => Rules(Magnet(tier)));
    }

    [Fact]
    public void Constructor_TiersMustBeAskedForMoreFoodsEachTime()
    {
        Assert.Throws<ArgumentException>(() => Rules(Magnet(Second, new PerkTier { FoodsRequired = 12, Multiplier = 3 })));
    }

    [Fact]
    public void Constructor_AtMostTenTiersInAll_TheFirstIncluded()
    {
        var tiers = Enumerable.Range(1, 10).Select(index => new PerkTier { FoodsRequired = 6 + index, Multiplier = 1 + index / 10.0 }).ToArray();

        Assert.Throws<ArgumentException>(() => Rules(Magnet(tiers)));
        Rules(Magnet(tiers.Take(9).ToArray()));
    }

    [Fact]
    public void BuiltInPerks_WithAmountsToScaleHaveTiersThatWork()
    {
        var rules = new PerkRules(new PerkOptions());

        var tiers = rules.TiersFor(1000);

        Assert.Contains(3, tiers);
        Assert.Contains(1, tiers);
        Assert.True(rules.EffectsFor(1000).MagnetRadius > rules.EffectsFor(6).MagnetRadius);
    }
}
