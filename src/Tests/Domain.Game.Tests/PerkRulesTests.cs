using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class PerkRulesTests
{
    private static PerkDefinition Perk(string id, int foods, PerkEffect effect, double amount)
        => new() { Id = id, Name = id, FoodsRequired = foods, Effect = effect, Amount = amount };

    private static PerkRules Rules(params PerkDefinition[] perks)
        => new(new PerkOptions { Perks = perks.ToList() });

    [Fact]
    public void UnlockedIds_BelowEveryThreshold_IsEmpty()
    {
        var rules = Rules(Perk("fast", 10, PerkEffect.BoostSpeed, 0.3));

        Assert.Empty(rules.UnlockedIds(9));
    }

    [Fact]
    public void UnlockedIds_AtTheThreshold_IncludesThePerk()
    {
        var rules = Rules(Perk("fast", 10, PerkEffect.BoostSpeed, 0.3));

        Assert.Equal(new[] { "fast" }, rules.UnlockedIds(10));
    }

    [Fact]
    public void UnlockedIds_KeepsTheOrderOfTheThresholds()
    {
        var rules = Rules(Perk("late", 20, PerkEffect.ExtraShots, 1), Perk("early", 5, PerkEffect.BoostSpeed, 0.2));

        Assert.Equal(new[] { "early", "late" }, rules.UnlockedIds(30));
    }

    [Fact]
    public void EffectsFor_NothingUnlocked_ChangesNothing()
    {
        var effects = Rules(Perk("fast", 10, PerkEffect.BoostSpeed, 0.3)).EffectsFor(0);

        Assert.Equal(0, effects.ExtraShots);
        Assert.Equal(0, effects.BoostSpeedBonus);
        Assert.Equal(1.0, effects.ReloadFactor);
        Assert.Equal(1.0, effects.BoostLengthFactor);
    }

    [Fact]
    public void EffectsFor_AddsUpPerksOfTheSameKind()
    {
        var rules = Rules(
            Perk("a", 1, PerkEffect.ExtraShots, 1),
            Perk("b", 2, PerkEffect.ExtraShots, 1),
            Perk("c", 3, PerkEffect.BoostSpeed, 0.2),
            Perk("d", 4, PerkEffect.BoostSpeed, 0.3));

        var effects = rules.EffectsFor(4);

        Assert.Equal(2, effects.ExtraShots);
        Assert.Equal(0.5, effects.BoostSpeedBonus, 6);
    }

    [Fact]
    public void EffectsFor_FasterReload_ShortensTheCooldown()
    {
        var effects = Rules(Perk("r", 1, PerkEffect.FasterReload, 0.4)).EffectsFor(1);

        Assert.Equal(0.6, effects.ReloadFactor, 6);
    }

    [Fact]
    public void EffectsFor_FasterReload_NeverGoesBelowAFifthOfTheCooldown()
    {
        var rules = Rules(
            Perk("a", 1, PerkEffect.FasterReload, 0.8),
            Perk("b", 2, PerkEffect.FasterReload, 0.8));

        Assert.Equal(0.2, rules.EffectsFor(2).ReloadFactor, 6);
    }

    [Fact]
    public void EffectsFor_EfficientBoost_MakesEachMassLastLonger()
    {
        var effects = Rules(Perk("e", 1, PerkEffect.EfficientBoost, 0.5)).EffectsFor(1);

        Assert.Equal(1.5, effects.BoostLengthFactor, 6);
    }

    [Fact]
    public void EffectsFor_Disabled_ChangesNothing()
    {
        var rules = new PerkRules(new PerkOptions
        {
            Enabled = false,
            Perks = [Perk("a", 1, PerkEffect.ExtraShots, 1)]
        });

        Assert.Equal(0, rules.EffectsFor(50).ExtraShots);
        Assert.Empty(rules.UnlockedIds(50));
    }

    [Fact]
    public void Constructor_WithoutPerks_UsesTheBuiltInSet()
    {
        var rules = new PerkRules(new PerkOptions());

        Assert.NotEmpty(rules.UnlockedIds(1000));
        Assert.True(rules.EffectsFor(1000).ExtraShots > 0);
    }

    [Fact]
    public void Constructor_RepeatedId_IsRejected()
    {
        Assert.Throws<ArgumentException>(() =>
            Rules(Perk("same", 1, PerkEffect.ExtraShots, 1), Perk("same", 2, PerkEffect.BoostSpeed, 0.1)));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(5, 0)]
    [InlineData(5, -1)]
    public void Constructor_NeedsAPositiveThresholdAndAmount(int foods, double amount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Rules(Perk("x", foods, PerkEffect.BoostSpeed, amount)));
    }

    [Theory]
    [InlineData(PerkEffect.FasterReload)]
    [InlineData(PerkEffect.EfficientBoost)]
    public void Constructor_RatioPerksCannotBeOneOrMore(PerkEffect effect)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Rules(Perk("x", 1, effect, 1.0)));
    }

    [Fact]
    public void ShotOffsets_OneShot_GoesStraight()
    {
        Assert.Equal(new[] { 0.0 }, new PerkRules(new PerkOptions()).ShotOffsets(1).ToArray());
    }

    [Fact]
    public void ShotOffsets_ManyShots_FanOutEvenlyAroundTheAim()
    {
        var spread = Math.PI / 12;
        var rules = new PerkRules(new PerkOptions { ShotSpreadDegrees = 15 });

        var offsets = rules.ShotOffsets(3).ToArray();

        Assert.Equal(new[] { -spread, 0, spread }.Select(o => Math.Round(o, 6)), offsets.Select(o => Math.Round(o, 6)));
    }

    [Fact]
    public void ShotOffsets_TwoShots_StraddleTheAim()
    {
        var rules = new PerkRules(new PerkOptions { ShotSpreadDegrees = 10 });

        var offsets = rules.ShotOffsets(2).ToArray();

        Assert.Equal(-offsets[1], offsets[0], 9);
        Assert.NotEqual(0, offsets[0]);
    }

    [Fact]
    public void EffectsFor_BurstShot_AddsShotsToTheBurst()
    {
        var rules = Rules(Perk("b", 1, PerkEffect.BurstShot, 2), Perk("c", 2, PerkEffect.BurstShot, 1));

        Assert.Equal(2, rules.EffectsFor(1).BurstExtraShots);
        Assert.Equal(3, rules.EffectsFor(2).BurstExtraShots);
    }

    [Fact]
    public void EffectsFor_ShotSpeed_MakesShotsFaster()
    {
        var effects = Rules(Perk("q", 1, PerkEffect.ShotSpeed, 0.5)).EffectsFor(1);

        Assert.Equal(1.5, effects.ShotSpeedFactor, 6);
    }

    [Fact]
    public void EffectsFor_AutoFireAndPiercing_AreSwitchesThatStayOn()
    {
        var rules = Rules(Perk("a", 1, PerkEffect.AutoFire, 1), Perk("p", 2, PerkEffect.PiercingShot, 1));

        Assert.True(rules.EffectsFor(1).AutoFire);
        Assert.False(rules.EffectsFor(1).Piercing);
        Assert.True(rules.EffectsFor(2).Piercing);
        Assert.False(PerkEffects.None.AutoFire);
    }

    [Fact]
    public void EffectsFor_Armor_CutsDamageAndNeverBelowAFifth()
    {
        var rules = Rules(Perk("a", 1, PerkEffect.Armor, 0.25), Perk("b", 2, PerkEffect.Armor, 0.9));

        Assert.Equal(0.75, rules.EffectsFor(1).DamageFactor, 6);
        Assert.Equal(0.2, rules.EffectsFor(2).DamageFactor, 6);
    }

    [Fact]
    public void EffectsFor_RegenerationAndMagnet_AddUp()
    {
        var rules = Rules(
            Perk("r1", 1, PerkEffect.Regeneration, 2),
            Perk("r2", 2, PerkEffect.Regeneration, 3),
            Perk("m1", 1, PerkEffect.Magnet, 50),
            Perk("m2", 2, PerkEffect.Magnet, 30));

        var effects = rules.EffectsFor(2);

        Assert.Equal(5, effects.RegenPerSecond, 6);
        Assert.Equal(80, effects.MagnetRadius, 6);
    }

    [Fact]
    public void Constructor_ArmorIsARatioBelowOne()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Rules(Perk("x", 1, PerkEffect.Armor, 1.0)));
    }

    [Fact]
    public void BurstIntervalMilliseconds_ComesFromTheOptions()
    {
        Assert.Equal(120, new PerkRules(new PerkOptions { BurstIntervalMilliseconds = 120 }).BurstIntervalMilliseconds);
    }

    [Fact]
    public void Constructor_BurstIntervalMustBePositive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PerkRules(new PerkOptions { BurstIntervalMilliseconds = 0 }));
    }

    [Fact]
    public void BuiltInSet_UsesEveryEffect()
    {
        var effects = PerkOptions.BuiltIn.Select(perk => perk.Effect).ToHashSet();

        Assert.Equal(Enum.GetValues<PerkEffect>().ToHashSet(), effects);
    }
}
