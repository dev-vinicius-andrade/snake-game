using Domain.Game.Rules;

namespace Domain.Game.Tests;

/// <summary>The perk effects that came with rewards: precision, extra lives, event resistance, plunder, cooldown abilities, and tier surges.</summary>
public sealed class PerkRewardEffectsTests
{
    private static PerkDefinition Perk(string id, int foods, PerkEffect effect, double amount, string? target = null, List<PerkTier>? tiers = null,
        int cooldown = 0, int duration = 0)
        => new()
        {
            Id = id, Name = id, FoodsRequired = foods, Effects = [new PerkEffectEntry { Effect = effect, Amount = amount, Target = target }],
            Tiers = tiers ?? [], CooldownSeconds = cooldown, DurationSeconds = duration
        };

    private static PerkRules Rules(params PerkDefinition[] perks) => new(new PerkOptions { Perks = perks.ToList() });

    [Fact]
    public void Precision_NarrowsTheVolley()
    {
        var effects = Rules(Perk("aim", 1, PerkEffect.Precision, 0.5)).EffectsFor(1);

        Assert.Equal(0.5, effects.SpreadFactor, 6);
    }

    [Fact]
    public void Precision_OfSeveralPerksMultiplies_AndNeverClosesTheFanCompletely()
    {
        var rules = Rules(Perk("a", 1, PerkEffect.Precision, 0.9), Perk("b", 1, PerkEffect.Precision, 0.9));

        Assert.Equal(0.1, rules.EffectsFor(1).SpreadFactor, 6);
    }

    [Fact]
    public void Precision_WithoutThePerk_LeavesTheSpreadAlone()
        => Assert.Equal(1.0, Rules(Perk("aim", 5, PerkEffect.Precision, 0.5)).EffectsFor(1).SpreadFactor);

    [Fact]
    public void ShotOffsets_ScaleWithTheSpreadFactor()
    {
        var rules = new PerkRules(new PerkOptions { ShotSpreadDegrees = 20, Perks = [Perk("aim", 1, PerkEffect.Precision, 0.5)] });

        var wide = rules.ShotOffsets(3).ToArray();
        var narrow = rules.ShotOffsets(3, 0.5).ToArray();

        Assert.Equal(wide[2] / 2, narrow[2], 9);
        Assert.Equal(0, narrow[1], 9);
    }

    [Fact]
    public void ExtraLife_AddsUpAndIsWholeLives()
    {
        var rules = Rules(Perk("a", 1, PerkEffect.ExtraLife, 1), Perk("b", 1, PerkEffect.ExtraLife, 1));

        Assert.Equal(2, rules.EffectsFor(1).ExtraLives);
    }

    [Fact]
    public void ExtraLife_ATierGivesMoreLives()
    {
        var rules = Rules(Perk("a", 1, PerkEffect.ExtraLife, 1, tiers: [new PerkTier { FoodsRequired = 10, Multiplier = 2 }]));

        Assert.Equal(1, rules.EffectsFor(5).ExtraLives);
        Assert.Equal(2, rules.EffectsFor(10).ExtraLives);
    }

    [Fact]
    public void EventResistance_WithoutATarget_CoversEveryEvent()
    {
        var effects = Rules(Perk("r", 1, PerkEffect.EventResistance, 0.4)).EffectsFor(1);

        Assert.All(new[] { "lightning", "meteors", "mines", "zone" }, kind => Assert.Equal(0.4, effects.ResistanceTo(kind), 6));
    }

    [Fact]
    public void EventResistance_WithATarget_CoversOnlyThatEvent()
    {
        var effects = Rules(Perk("r", 1, PerkEffect.EventResistance, 0.6, target: "zone")).EffectsFor(1);

        Assert.Equal(0.6, effects.ResistanceTo("zone"), 6);
        Assert.Equal(0, effects.ResistanceTo("lightning"));
    }

    [Fact]
    public void EventResistance_Stacks_AsSharesOfWhatIsLeft()
    {
        var rules = Rules(Perk("a", 1, PerkEffect.EventResistance, 0.5), Perk("b", 1, PerkEffect.EventResistance, 0.5, target: "mines"));

        Assert.Equal(0.75, rules.EffectsFor(1).ResistanceTo("mines"), 6);
        Assert.Equal(0.5, rules.EffectsFor(1).ResistanceTo("zone"), 6);
    }

    [Fact]
    public void EventResistance_NeverMakesASnakeImmune_EvenAtAHighTier()
    {
        var rules = Rules(Perk("r", 1, PerkEffect.EventResistance, 0.5, tiers: [new PerkTier { FoodsRequired = 5, Multiplier = 10 }]));

        Assert.Equal(0.95, rules.EffectsFor(5).ResistanceTo("zone"), 6);
    }

    [Fact]
    public void Plunder_IsTheShareOfAKilledSnakeMass()
        => Assert.Equal(0.25, Rules(Perk("p", 1, PerkEffect.Plunder, 0.25)).EffectsFor(1).PlunderShare, 6);

    [Fact]
    public void AbilitiesFor_ListsThePerksWithCooldownAbilities_ScaledByTier()
    {
        var rules = Rules(
            Perk("dash", 1, PerkEffect.Dash, 1, tiers: [new PerkTier { FoodsRequired = 10, Multiplier = 2 }], cooldown: 12, duration: 2),
            Perk("armor", 1, PerkEffect.Armor, 0.2));

        var low = Assert.Single(rules.AbilitiesFor(5));
        var high = Assert.Single(rules.AbilitiesFor(10));

        Assert.Equal(("dash", PerkEffect.Dash, 1.0, 12000, 2000), (low.PerkId, low.Effect, low.Amount, low.CooldownMilliseconds, low.DurationMilliseconds));
        Assert.Equal(2.0, high.Amount);
    }

    [Fact]
    public void AbilitiesFor_APerkNotYetEarned_IsNotListed()
        => Assert.Empty(Rules(Perk("shield", 20, PerkEffect.ShieldBurst, 1, cooldown: 30, duration: 3)).AbilitiesFor(5));

    [Fact]
    public void AbilitiesFor_AGrantedPerk_IsListed()
        => Assert.Single(Rules(Perk("shield", 20, PerkEffect.ShieldBurst, 1, cooldown: 30, duration: 3)).AbilitiesFor(0, granted: ["shield"]));

    [Fact]
    public void CooldownAbilities_NeedACooldown()
        => Assert.Throws<ArgumentException>(() => Rules(Perk("shield", 1, PerkEffect.ShieldBurst, 1, duration: 3)));

    [Theory]
    [InlineData(PerkEffect.ShieldBurst)]
    [InlineData(PerkEffect.Dash)]
    [InlineData(PerkEffect.VacuumBurst)]
    public void LastingAbilities_NeedADuration(PerkEffect effect)
        => Assert.Throws<ArgumentException>(() => Rules(Perk("a", 1, effect, 1, cooldown: 10)));

    [Fact]
    public void HealPulse_IsInstant_SoNeedsNoDuration()
        => Rules(Perk("pulse", 1, PerkEffect.HealPulse, 50, cooldown: 20));

    [Fact]
    public void ATarget_IsOnlyForEventResistance_AndMustBeAnEvent()
    {
        Assert.Throws<ArgumentException>(() => Rules(Perk("r", 1, PerkEffect.EventResistance, 0.5, target: "snow")));
        Assert.Throws<ArgumentException>(() => Rules(Perk("r", 1, PerkEffect.Armor, 0.5, target: "zone")));
    }

    [Theory]
    [InlineData(PerkEffect.Precision)]
    [InlineData(PerkEffect.EventResistance)]
    [InlineData(PerkEffect.Plunder)]
    public void TheNewShares_MustBeBelowOne(PerkEffect effect)
        => Assert.Throws<ArgumentOutOfRangeException>(() => Rules(Perk("a", 1, effect, 1)));

    [Fact]
    public void ExtraLife_MoreThanTheMostLivesIsRefused()
        => Assert.Throws<ArgumentOutOfRangeException>(() => Rules(Perk("a", 1, PerkEffect.ExtraLife, 6)));

    // Tier surges. The rule: the tier is the tier the foods give plus the tiers a reward added, but never past the last tier of the perk.

    private static readonly PerkTier[] ThreeMore =
    [
        new() { FoodsRequired = 10, Multiplier = 1.5 }, new() { FoodsRequired = 20, Multiplier = 2 }, new() { FoodsRequired = 30, Multiplier = 3 }
    ];

    private static PerkRules Magnet() => Rules(Perk("magnet", 5, PerkEffect.Magnet, 100, tiers: ThreeMore.ToList()));

    private static Dictionary<string, int> Surge(int tiers) => new() { ["magnet"] = tiers };

    [Fact]
    public void TiersFor_ASurgeAddsToTheTierTheFoodsGive()
    {
        var rules = Magnet();

        Assert.Equal([1], rules.TiersFor(5));
        Assert.Equal([3], rules.TiersFor(5, surges: Surge(2)));
        Assert.Equal([4], rules.TiersFor(10, surges: Surge(2)));
    }

    [Fact]
    public void TiersFor_ASurgeStopsAtTheLastTier()
        => Assert.Equal([4], Magnet().TiersFor(30, surges: Surge(9)));

    [Fact]
    public void EffectsFor_ASurgeUsesTheMultiplierOfTheTierItReaches()
    {
        var rules = Magnet();

        Assert.Equal(100, rules.EffectsFor(5).MagnetRadius);
        Assert.Equal(200, rules.EffectsFor(5, surges: Surge(2)).MagnetRadius);
        Assert.Equal(300, rules.EffectsFor(5, surges: Surge(3)).MagnetRadius);
    }

    [Fact]
    public void ASurge_OfAPerkTheSnakeDoesNotHave_DoesNothing()
    {
        var rules = Magnet();

        Assert.Empty(rules.TiersFor(2, surges: Surge(3)));
        Assert.Equal(0, rules.EffectsFor(2, surges: Surge(3)).MagnetRadius);
    }

    [Fact]
    public void ASurge_KeepsWorkingAsFoodsGrow()
    {
        var rules = Magnet();
        var surge = Surge(1);

        Assert.Equal([2], rules.TiersFor(5, surges: surge));
        Assert.Equal([3], rules.TiersFor(10, surges: surge));
    }

    [Fact]
    public void SurgeCandidates_AreTheOwnedPerksThatCanStillGoUp()
    {
        var rules = Rules(
            Perk("magnet", 5, PerkEffect.Magnet, 100, tiers: ThreeMore.ToList()),
            Perk("flat", 5, PerkEffect.Magnet, 10),
            Perk("late", 500, PerkEffect.Magnet, 100, tiers: ThreeMore.Select(tier => tier with { FoodsRequired = tier.FoodsRequired + 500 }).ToList()));

        Assert.Equal(["magnet"], rules.SurgeCandidates(5));
        Assert.Empty(rules.SurgeCandidates(5, surges: Surge(3)));
    }

    [Fact]
    public void UnownedPerks_AreThoseThatWorkHereAndAreNotEarnedOrGranted()
    {
        var rules = Rules(Perk("a", 1, PerkEffect.Magnet, 10), Perk("b", 50, PerkEffect.Armor, 0.1), Perk("c", 60, PerkEffect.Armor, 0.1));

        Assert.Equal(["c"], rules.UnownedPerkIds(5, granted: ["b"]));
    }

    [Fact]
    public void UnownedPerks_LeaveOutPerksThatAreOff()
    {
        var off = Perk("off", 50, PerkEffect.Magnet, 10) with { Enabled = false };

        Assert.Empty(Rules(off).UnownedPerkIds(0));
    }

    // Reward-only tiers: eating never reaches them, only a surge does.

    /// <summary>Five eatable tiers (the perk and four more, at 10, 20, 30, 40 foods) and two reward-only ones.</summary>
    private static PerkRules SevenTiers() => Rules(Perk("magnet", 5, PerkEffect.Magnet, 100, tiers:
    [
        new() { FoodsRequired = 10, Multiplier = 1.5 }, new() { FoodsRequired = 20, Multiplier = 2 }, new() { FoodsRequired = 30, Multiplier = 3 },
        new() { FoodsRequired = 40, Multiplier = 4 },
        new() { FoodsRequired = 50, Multiplier = 6, RewardOnly = true }, new() { FoodsRequired = 60, Multiplier = 9, RewardOnly = true }
    ]));

    [Fact]
    public void RewardOnly_EatingNeverGoesPastTheLastEatableTier()
    {
        var rules = SevenTiers();

        Assert.Equal([5], rules.TiersFor(40));
        Assert.Equal([5], rules.TiersFor(100000));
        Assert.Equal(400, rules.EffectsFor(100000).MagnetRadius);
    }

    [Theory]
    [InlineData(1, 6, 600)]
    [InlineData(2, 7, 900)]
    [InlineData(5, 7, 900)]
    public void RewardOnly_ASurgeFromTheLastEatableTierReachesThem_AndStopsAtTheLastOne(int surge, int tier, double radius)
    {
        var rules = SevenTiers();

        Assert.Equal([tier], rules.TiersFor(40, surges: Surge(surge)));
        Assert.Equal(radius, rules.EffectsFor(40, surges: Surge(surge)).MagnetRadius);
    }

    [Fact]
    public void RewardOnly_ASurgeStillAddsToWhatFoodsGive_BeforeTheEnd()
    {
        var rules = SevenTiers();

        Assert.Equal([4], rules.TiersFor(10, surges: Surge(2)));
        Assert.Equal([7], rules.TiersFor(30, surges: Surge(9)));
    }

    [Fact]
    public void RewardOnly_ThePerkCanStillBeLifted_UntilItIsAtTheLastTier()
    {
        var rules = SevenTiers();

        Assert.Equal(["magnet"], rules.SurgeCandidates(40));
        Assert.Equal(["magnet"], rules.SurgeCandidates(40, surges: Surge(1)));
        Assert.Empty(rules.SurgeCandidates(40, surges: Surge(2)));
        Assert.Equal(7, rules.TierCountOf("magnet"));
    }

    [Fact]
    public void RewardOnly_APerkCanHaveTenTiers_AndNoMore()
    {
        PerkTier Step(int n) => new() { FoodsRequired = 10 * n, Multiplier = 1 + n / 10.0, RewardOnly = n > 4 };

        Rules(Perk("m", 5, PerkEffect.Magnet, 1, tiers: Enumerable.Range(1, 9).Select(Step).ToList()));
        Assert.Throws<ArgumentException>(() => Rules(Perk("m", 5, PerkEffect.Magnet, 1, tiers: Enumerable.Range(1, 10).Select(Step).ToList())));
    }

    [Fact]
    public void RewardOnly_MustComeAfterEveryEatableTier()
    {
        var tiers = new List<PerkTier>
        {
            new() { FoodsRequired = 10, Multiplier = 2, RewardOnly = true }, new() { FoodsRequired = 20, Multiplier = 3 }
        };

        var failure = Assert.Throws<ArgumentException>(() => Rules(Perk("m", 5, PerkEffect.Magnet, 1, tiers: tiers)));
        Assert.Contains("reward-only", failure.Message);
    }

    [Fact]
    public void RewardOnly_OnlyRewardOnlyTiers_ArePerfectlyFine()
    {
        var rules = Rules(Perk("magnet", 5, PerkEffect.Magnet, 100, tiers: [new PerkTier { FoodsRequired = 10, Multiplier = 2, RewardOnly = true }]));

        Assert.Equal([1], rules.TiersFor(1000));
        Assert.Equal([2], rules.TiersFor(1000, surges: Surge(1)));
    }

    [Fact]
    public void RewardOnly_ConfigWithoutTheFlag_ReadsAsEatable()
    {
        var json = System.Text.Json.JsonSerializer.Deserialize<PerkTier>("{\"foodsRequired\":9,\"multiplier\":2}", GameplayConfig.Json)!;

        Assert.False(json.RewardOnly);
    }
}
