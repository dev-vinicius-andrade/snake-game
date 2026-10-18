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
}
