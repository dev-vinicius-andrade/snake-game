using Domain.Game.Scripting;
using NSubstitute;
using Library.Commons.Game.Domain.Interfaces.Entities;
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
        var effects = PerkOptions.BuiltIn.SelectMany(perk => perk.AllEffects()).Select(entry => entry.Effect).ToHashSet();

        Assert.Equal(Enum.GetValues<PerkEffect>().ToHashSet(), effects);
    }

    [Fact]
    public void Reconfigure_ChangesWhatPerksDoFromNowOn()
    {
        var rules = Rules(Perk("a", 1, PerkEffect.ExtraShots, 1));

        rules.Reconfigure(new PerkOptions { Perks = [Perk("b", 1, PerkEffect.BoostSpeed, 0.5)] });

        Assert.Equal(0, rules.EffectsFor(1).ExtraShots);
        Assert.Equal(0.5, rules.EffectsFor(1).BoostSpeedBonus, 6);
        Assert.Equal(new[] { "b" }, rules.UnlockedIds(1));
    }

    [Fact]
    public void Reconfigure_WithBadOptions_ThrowsAndKeepsTheOldOnes()
    {
        var rules = Rules(Perk("a", 1, PerkEffect.ExtraShots, 1));

        Assert.Throws<ArgumentOutOfRangeException>(() => rules.Reconfigure(new PerkOptions { Perks = [Perk("b", 0, PerkEffect.BoostSpeed, 0.5)] }));

        Assert.Equal(1, rules.EffectsFor(1).ExtraShots);
    }

    [Fact]
    public void Reconfigure_ChangesTheBurstIntervalAndTheSpread()
    {
        var rules = new PerkRules(new PerkOptions());

        rules.Reconfigure(new PerkOptions { BurstIntervalMilliseconds = 200, ShotSpreadDegrees = 20 });

        Assert.Equal(200, rules.BurstIntervalMilliseconds);
        Assert.Equal(20 * Math.PI / 180, rules.ShotOffsets(2).Last() * 2, 9);
    }

    [Fact]
    public void Settings_AreWhatWasLastApplied()
    {
        var options = new PerkOptions { Perks = [Perk("a", 3, PerkEffect.Magnet, 40)] };
        var rules = new PerkRules(options);

        Assert.Same(options, rules.Options);
    }

    private static PerkDefinition Multi(string id, int foods, params PerkEffectEntry[] effects)
        => new() { Id = id, Name = id, FoodsRequired = foods, Effects = effects.ToList() };

    private static PerkEffectEntry Entry(PerkEffect effect, double amount) => new() { Effect = effect, Amount = amount };

    private static IRoom Room(string? code = null, string? name = null, Guid? guid = null)
    {
        var room = Substitute.For<IRoom>();
        room.Code.Returns(code);
        room.Name.Returns(name);
        room.Guid.Returns(guid ?? Guid.NewGuid());
        return room;
    }

    [Fact]
    public void EffectsFor_APerkWithSeveralEffects_GivesAllOfThem()
    {
        var rules = new PerkRules(new PerkOptions
        {
            Perks = [Multi("berserker", 5, Entry(PerkEffect.ExtraShots, 2), Entry(PerkEffect.FasterReload, 0.3), Entry(PerkEffect.BoostSpeed, 0.4))]
        });

        var effects = rules.EffectsFor(5);

        Assert.Equal(2, effects.ExtraShots);
        Assert.Equal(0.7, effects.ReloadFactor, 6);
        Assert.Equal(0.4, effects.BoostSpeedBonus, 6);
    }

    [Fact]
    public void EffectsFor_SeveralPerksAndSeveralEffectsAddUpTogether()
    {
        var rules = new PerkRules(new PerkOptions
        {
            Perks = [Multi("a", 1, Entry(PerkEffect.ExtraShots, 1), Entry(PerkEffect.Magnet, 30)), Multi("b", 2, Entry(PerkEffect.ExtraShots, 1), Entry(PerkEffect.Magnet, 20))]
        });

        var effects = rules.EffectsFor(2);

        Assert.Equal(2, effects.ExtraShots);
        Assert.Equal(50, effects.MagnetRadius, 6);
    }

    [Fact]
    public void AllEffects_UsesTheListAndFallsBackToTheSingleOldEffect()
    {
        var single = new PerkDefinition { Id = "s", FoodsRequired = 1, Effect = PerkEffect.Magnet, Amount = 5 };
        var several = new PerkDefinition { Id = "m", FoodsRequired = 1, Effect = PerkEffect.Magnet, Amount = 5, Effects = [Entry(PerkEffect.Armor, 0.2)] };
        var none = new PerkDefinition { Id = "n", FoodsRequired = 1 };

        Assert.Equal(new[] { PerkEffect.Magnet }, single.AllEffects().Select(entry => entry.Effect));
        Assert.Equal(new[] { PerkEffect.Armor }, several.AllEffects().Select(entry => entry.Effect));
        Assert.Empty(none.AllEffects());
    }

    [Fact]
    public void Constructor_APerkWithNoEffectAtAll_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => new PerkRules(new PerkOptions { Perks = [new PerkDefinition { Id = "x", Name = "x", FoodsRequired = 1 }] }));
    }

    [Fact]
    public void Constructor_EveryEffectOfAPerkIsChecked()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PerkRules(new PerkOptions { Perks = [Multi("x", 1, Entry(PerkEffect.ExtraShots, 1), Entry(PerkEffect.Armor, 1.5))] }));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PerkRules(new PerkOptions { Perks = [Multi("x", 1, Entry(PerkEffect.ExtraShots, 1), Entry(PerkEffect.Magnet, 0))] }));
    }

    [Fact]
    public void Rooms_APerkWithRoomsOnlyWorksInThoseRooms_ByCodeNameOrGuid()
    {
        var guid = Guid.NewGuid();
        var rules = new PerkRules(new PerkOptions
        {
            Perks =
            [
                new PerkDefinition { Id = "by-code", FoodsRequired = 1, Effect = PerkEffect.Magnet, Amount = 10, Rooms = ["abc234"] },
                new PerkDefinition { Id = "by-name", FoodsRequired = 1, Effect = PerkEffect.Magnet, Amount = 20, Rooms = ["Friends"] },
                new PerkDefinition { Id = "by-guid", FoodsRequired = 1, Effect = PerkEffect.Magnet, Amount = 40, Rooms = [guid.ToString()] },
                new PerkDefinition { Id = "everywhere", FoodsRequired = 1, Effect = PerkEffect.Magnet, Amount = 80 },
            ]
        });

        Assert.Equal(new[] { "everywhere" }, rules.UnlockedIds(1, Room(code: "OTHER1")));
        Assert.Equal(new[] { "by-code", "everywhere" }, rules.UnlockedIds(1, Room(code: "ABC234")));
        Assert.Equal(new[] { "by-name", "everywhere" }, rules.UnlockedIds(1, Room(name: "friends")));
        Assert.Equal(new[] { "by-guid", "everywhere" }, rules.UnlockedIds(1, Room(guid: guid)));
        Assert.Equal(new[] { "everywhere" }, rules.UnlockedIds(1));
    }

    [Fact]
    public void Rooms_EffectsFollowTheRoomToo()
    {
        var rules = new PerkRules(new PerkOptions
        {
            Perks = [new PerkDefinition { Id = "a", FoodsRequired = 1, Effect = PerkEffect.Magnet, Amount = 99, Rooms = ["ABC234"] }]
        });

        Assert.Equal(99, rules.EffectsFor(1, Room(code: "ABC234")).MagnetRadius);
        Assert.Equal(0, rules.EffectsFor(1, Room(code: "OTHER1")).MagnetRadius);
    }

    [Fact]
    public void AppliesTo_APerkWithAScriptIsOffEverywhereUntilRoomsAreNamed()
    {
        var scripted = new PerkDefinition { Id = "s", FoodsRequired = 1, Script = "function modify(){return {};}" };
        var scopedScript = scripted with { Rooms = ["ABC234"] };

        Assert.False(scripted.AppliesTo(Room(code: "ABC234")));
        Assert.True(scopedScript.AppliesTo(Room(code: "ABC234")));
        Assert.False(scopedScript.AppliesTo(Room(code: "OTHER1")));
        Assert.False(scopedScript.AppliesTo(null));
    }

    [Fact]
    public void Constructor_ARoomEntryThatIsBlankIsRefused()
    {
        Assert.Throws<ArgumentException>(() => new PerkRules(new PerkOptions
        {
            Perks = [new PerkDefinition { Id = "a", FoodsRequired = 1, Effect = PerkEffect.Magnet, Amount = 1, Rooms = [" "] }]
        }));
    }

    private static readonly ScriptSnake ASnake = new("ana", 12, 30, 9, 1.0, 1.0, false, []);
    private static readonly ScriptRoom ARoom = new(2, "Friends", "ABC234", 1920, 1080);
    private static readonly ScriptContext Here = new(ASnake, ARoom);

    private static PerkDefinition Scripted(string id, string script, params string[] rooms)
        => new() { Id = id, Name = id, FoodsRequired = 1, Script = script, Rooms = rooms.ToList() };

    private static PerkRules WithPerks(params PerkDefinition[] perks) => new(new PerkOptions { Perks = perks.ToList() });

    [Fact]
    public void Scripts_AddToWhatTheEffectsGive_InTheRoomsTheyNameOnly()
    {
        var rules = WithPerks(new PerkDefinition
        {
            Id = "s", Name = "s", FoodsRequired = 1, Effects = [Entry(PerkEffect.ExtraShots, 1)], Rooms = ["ABC234"],
            Script = "function modify(snake) { return { extraShots: 2, regenPerSecond: 3 }; }"
        });

        var inside = rules.EffectsFor(1, Room(code: "ABC234"), Here);
        var outside = rules.EffectsFor(1, Room(code: "OTHER1"), Here);

        Assert.Equal(3, inside.ExtraShots);
        Assert.Equal(3, inside.RegenPerSecond);
        Assert.Equal(0, outside.ExtraShots);
        Assert.Equal(0, outside.RegenPerSecond);
    }

    [Fact]
    public void Scripts_AreOffEverywhereWhenNoRoomIsNamed()
    {
        var rules = WithPerks(Scripted("s", "function modify() { return { extraShots: 4 }; }"));

        Assert.Equal(0, rules.EffectsFor(1, Room(code: "ABC234"), Here).ExtraShots);
        Assert.Empty(rules.UnlockedIds(1, Room(code: "ABC234")));
    }

    [Fact]
    public void Scripts_FactorsMultiplyAndStayInRange()
    {
        var rules = WithPerks(
            Scripted("a", "function modify() { return { reloadFactor: 0.5, damageFactor: 0.5, shotSpeedFactor: 2, boostLengthFactor: 2 }; }", "ABC234"),
            Scripted("b", "function modify() { return { reloadFactor: 0.2, damageFactor: 0.2, shotSpeedFactor: 2, boostLengthFactor: 2 }; }", "ABC234"));

        var effects = rules.EffectsFor(1, Room(code: "ABC234"), Here);

        Assert.Equal(0.2, effects.ReloadFactor, 6);
        Assert.Equal(0.2, effects.DamageFactor, 6);
        Assert.Equal(4, effects.ShotSpeedFactor, 6);
        Assert.Equal(4, effects.BoostLengthFactor, 6);
    }

    [Fact]
    public void Scripts_SwitchesAreOrdered()
    {
        var rules = WithPerks(Scripted("a", "function modify() { return { autoFire: true, piercing: false }; }", "ABC234"));

        var effects = rules.EffectsFor(1, Room(code: "ABC234"), Here);

        Assert.True(effects.AutoFire);
        Assert.False(effects.Piercing);
    }

    [Fact]
    public void Scripts_ThatFailLeaveTheEffectsOfThePerkAlone()
    {
        var rules = WithPerks(new PerkDefinition
        {
            Id = "s", Name = "s", FoodsRequired = 1, Effects = [Entry(PerkEffect.ExtraShots, 1)], Rooms = ["ABC234"],
            Script = "function modify(snake) { if (snake.name === 'ana') throw new Error('boom'); return {}; }"
        });

        Assert.Equal(1, rules.EffectsFor(1, Room(code: "ABC234"), Here).ExtraShots);
    }

    [Fact]
    public void Scripts_ThatKeepFailingAreSwitchedOffAndNoLongerCalled()
    {
        var rules = WithPerks(Scripted("s", "function modify(snake) { if (snake.name === 'ana') throw new Error('boom'); return {}; }", "ABC234"));
        var room = Room(code: "ABC234");

        for (var call = 0; call < 5; call++) rules.EffectsFor(1, room, Here);

        var script = rules.ScriptOf("s")!;
        Assert.True(script.Disabled);
        Assert.Equal(PerkScript.MaxFailuresInARow, script.Failures);
    }

    [Fact]
    public void RewardsForEating_AddsUpWhatTheScriptsGive_InTheirRoomsOnly()
    {
        var rules = WithPerks(
            Scripted("a", "function onEat(snake, food) { return { mass: 2, heal: 10 }; }", "ABC234"),
            Scripted("b", "function onEat(snake, food) { return { mass: 3, heal: 5 }; }", "ABC234"),
            Scripted("c", "function onEat(snake, food) { return { mass: 40 }; }", "OTHER1"));

        var rewards = rules.RewardsForEating(1, Room(code: "ABC234"), Here, new ScriptFood(1, false));

        Assert.Equal(5, rewards.Mass);
        Assert.Equal(15, rewards.Heal, 6);
    }

    [Fact]
    public void RewardsForEating_WithoutScriptsIsNothing()
    {
        var rewards = new PerkRules(new PerkOptions()).RewardsForEating(100, Room(code: "ABC234"), Here, new ScriptFood(1, false));

        Assert.Equal(new ScriptRewards(0, 0), rewards);
    }

    [Fact]
    public void HasScripts_SaysWhetherAnyScriptWorksForThisSnakeHere()
    {
        var rules = WithPerks(Scripted("s", "function modify() { return {}; }", "ABC234"), new PerkDefinition { Id = "n", Name = "n", FoodsRequired = 1, Effects = [Entry(PerkEffect.Magnet, 5)] });

        Assert.True(rules.HasScripts(1, Room(code: "ABC234")));
        Assert.False(rules.HasScripts(1, Room(code: "OTHER1")));
        Assert.False(rules.HasScripts(0, Room(code: "ABC234")));
    }

    [Fact]
    public void Constructor_AScriptThatDoesNotCompileIsRefused_NamingThePerk()
    {
        var failure = Assert.Throws<ArgumentException>(() => WithPerks(Scripted("broken", "function modify( {", "ABC234")));

        Assert.Contains("broken", failure.Message);
    }

    [Fact]
    public void Constructor_AScriptThatFailsItsCheckIsRefused()
    {
        var failure = Assert.Throws<ArgumentException>(() => WithPerks(Scripted("bad", "function modify(snake) { return snake.nothing.deeper; }", "ABC234")));

        Assert.Contains("bad", failure.Message);
    }

    [Fact]
    public void Constructor_APerkWithOnlyAScriptIsFine()
    {
        var rules = WithPerks(Scripted("only", "function modify() { return { extraShots: 1 }; }", "ABC234"));

        Assert.Equal(1, rules.EffectsFor(1, Room(code: "ABC234"), Here).ExtraShots);
    }

    [Fact]
    public void Reconfigure_StartsTheScriptsAgain_SoOldFailuresAreForgotten()
    {
        var rules = WithPerks(Scripted("s", "function modify(snake) { if (snake.name === 'ana') throw new Error('boom'); return {}; }", "ABC234"));
        var room = Room(code: "ABC234");
        for (var call = 0; call < 3; call++) rules.EffectsFor(1, room, Here);
        Assert.True(rules.ScriptOf("s")!.Disabled);

        rules.Reconfigure(new PerkOptions { Perks = [Scripted("s", "function modify() { return { extraShots: 1 }; }", "ABC234")] });

        Assert.False(rules.ScriptOf("s")!.Disabled);
        Assert.Equal(1, rules.EffectsFor(1, room, Here).ExtraShots);
    }

    [Fact]
    public void Reconfigure_ABrokenScriptIsRefusedAndTheOldOnesKeepWorking()
    {
        var rules = WithPerks(Scripted("s", "function modify() { return { extraShots: 1 }; }", "ABC234"));

        Assert.Throws<ArgumentException>(() => rules.Reconfigure(new PerkOptions { Perks = [Scripted("s", "function modify( {", "ABC234")] }));

        Assert.Equal(1, rules.EffectsFor(1, Room(code: "ABC234"), Here).ExtraShots);
    }
}
