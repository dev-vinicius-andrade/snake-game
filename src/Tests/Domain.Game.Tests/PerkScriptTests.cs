using System.Diagnostics;
using Domain.Game.Scripting;

namespace Domain.Game.Tests;

public sealed class PerkScriptTests
{
    private static readonly ScriptSnake Snake = new("ana", Length: 12, Mass: 30, FoodsEaten: 9, Health: 0.8, LowestHealth: 0.4, Boosting: true, Perks: ["swift"]);
    private static readonly ScriptRoom Room = new(Players: 3, Name: "Friends", Code: "ABC234", Width: 1920, Height: 1080);
    private static readonly ScriptFood Food = new(Value: 1, IsPrize: false);

    private static PerkScript Compile(string source) => PerkScript.Compile(source);

    [Fact]
    public void Compile_ASyntaxError_SaysWhatAndWhere()
    {
        var failure = Assert.Throws<ScriptException>(() => Compile("function modify( {"));

        Assert.NotEmpty(failure.Message);
    }

    [Fact]
    public void Compile_AScriptWithNeitherFunctionIsRefused()
    {
        var failure = Assert.Throws<ScriptException>(() => Compile("var x = 1;"));

        Assert.Contains("modify", failure.Message);
        Assert.Contains("onEat", failure.Message);
    }

    [Fact]
    public void Modify_GivesWhatTheScriptReturns()
    {
        var script = Compile("function modify(snake, room) { return { extraShots: 2, reloadFactor: 0.5, autoFire: true }; }");

        var result = script.Modify(Snake, Room)!;

        Assert.Equal(2, result.ExtraShots);
        Assert.Equal(0.5, result.ReloadFactor);
        Assert.True(result.AutoFire);
        Assert.Null(result.Piercing);
    }

    [Fact]
    public void Modify_SeesTheSnakeAndTheRoom()
    {
        var script = Compile(@"
            function modify(snake, room) {
              return {
                extraShots: snake.length === 12 && snake.name === 'ana' && snake.boosting && snake.perks[0] === 'swift' ? 1 : 0,
                regenPerSecond: room.players === 3 && room.code === 'ABC234' && snake.health === 0.8 && snake.lowestHealth === 0.4 ? 7 : 0
              };
            }");

        var result = script.Modify(Snake, Room)!;

        Assert.Equal(1, result.ExtraShots);
        Assert.Equal(7, result.RegenPerSecond);
    }

    [Theory]
    [InlineData("extraShots: 99", "ExtraShots", 4)]
    [InlineData("extraShots: -3", "ExtraShots", 0)]
    [InlineData("burstExtraShots: 50", "BurstExtraShots", 6)]
    [InlineData("boostSpeedBonus: 10", "BoostSpeedBonus", 3)]
    [InlineData("boostLengthFactor: 0.1", "BoostLengthFactor", 1)]
    [InlineData("boostLengthFactor: 100", "BoostLengthFactor", 4)]
    [InlineData("reloadFactor: 0", "ReloadFactor", 0.2)]
    [InlineData("reloadFactor: 5", "ReloadFactor", 1)]
    [InlineData("shotSpeedFactor: 50", "ShotSpeedFactor", 4)]
    [InlineData("damageFactor: 0", "DamageFactor", 0.2)]
    [InlineData("regenPerSecond: 1000", "RegenPerSecond", 50)]
    [InlineData("magnetRadius: 100000", "MagnetRadius", 300)]
    public void Modify_KeepsEveryValueInItsRange(string returned, string field, double expected)
    {
        var result = Compile($"function modify() {{ return {{ {returned} }}; }}").Modify(Snake, Room)!;

        var actual = Convert.ToDouble(typeof(ScriptEffects).GetProperty(field)!.GetValue(result));
        Assert.Equal(expected, actual, 6);
    }

    [Fact]
    public void Modify_IgnoresWhatIsNotANumberOrABoolean()
    {
        var script = Compile("function modify() { return { extraShots: 'lots', reloadFactor: NaN, magnetRadius: Infinity, autoFire: 1, unknown: 5 }; }");

        var result = script.Modify(Snake, Room)!;

        Assert.Null(result.ExtraShots);
        Assert.Null(result.ReloadFactor);
        Assert.Null(result.MagnetRadius);
        Assert.Null(result.AutoFire);
    }

    [Fact]
    public void Modify_AScriptWithoutTheFunctionDoesNothing()
    {
        var script = Compile("function onEat() { return { mass: 1 }; }");

        Assert.Null(script.Modify(Snake, Room));
        Assert.False(script.Failed);
    }

    [Fact]
    public void Modify_ReturningNothingGivesNoEffects()
    {
        var script = Compile("function modify() { }");

        var result = script.Modify(Snake, Room);

        Assert.True(result is null || result == new ScriptEffects());
        Assert.False(script.Failed);
    }

    [Fact]
    public void OnEat_GivesMassAndHealing_InsideTheirRanges()
    {
        var script = Compile("function onEat(snake, food, room) { return { mass: food.isPrize ? 100 : 3, heal: 9999 }; }");

        var plain = script.OnEat(Snake, Food, Room)!;
        var prize = script.OnEat(Snake, new ScriptFood(20, true), Room)!;

        Assert.Equal(3, plain.Mass);
        Assert.Equal(500, plain.Heal);
        Assert.Equal(50, prize.Mass);
    }

    [Fact]
    public void OnEat_NegativeValuesAreNothing()
    {
        var script = Compile("function onEat() { return { mass: -5, heal: -1 }; }");

        var result = script.OnEat(Snake, Food, Room)!;

        Assert.Equal(0, result.Mass);
        Assert.Equal(0, result.Heal);
    }

    [Fact]
    public void ACallThatThrows_IsSkippedAndCounted()
    {
        var script = Compile("function modify() { throw new Error('boom'); }");

        var result = script.Modify(Snake, Room);

        Assert.Null(result);
        Assert.Equal(1, script.Failures);
        Assert.Contains("boom", script.LastError);
    }

    [Fact]
    public void ALoopThatNeverEnds_IsStoppedQuickly()
    {
        var script = Compile("function modify() { while (true) {} }");
        var clock = Stopwatch.StartNew();

        var result = script.Modify(Snake, Room);

        Assert.Null(result);
        Assert.True(clock.ElapsedMilliseconds < 1000, $"took {clock.ElapsedMilliseconds} ms");
        Assert.Equal(1, script.Failures);
    }

    [Fact]
    public void ARunawayRecursion_IsStopped()
    {
        var script = Compile("function down(n) { return down(n + 1); } function modify() { return { extraShots: down(0) }; }");

        Assert.Null(script.Modify(Snake, Room));
        Assert.Equal(1, script.Failures);
    }

    [Fact]
    public void AMemoryHog_IsStopped()
    {
        var script = Compile("function modify() { var a = []; while (true) { a.push(new Array(100000).fill('x')); } }");

        Assert.Null(script.Modify(Snake, Room));
        Assert.Equal(1, script.Failures);
    }

    [Fact]
    public void ThreeFailuresInARow_SwitchTheScriptOff()
    {
        var script = Compile("function modify() { throw new Error('boom'); }");

        script.Modify(Snake, Room);
        script.Modify(Snake, Room);
        Assert.False(script.Disabled);
        script.Modify(Snake, Room);

        Assert.True(script.Disabled);
        Assert.Null(script.Modify(Snake, Room));
        Assert.Equal(3, script.Failures);
    }

    [Fact]
    public void AGoodCall_ClearsTheCountOfFailures()
    {
        var script = Compile("function modify(snake) { if (snake.length === 1) throw new Error('boom'); return { extraShots: 1 }; }");
        var bad = Snake with { Length = 1 };

        script.Modify(bad, Room);
        script.Modify(bad, Room);
        script.Modify(Snake, Room);
        script.Modify(bad, Room);
        script.Modify(bad, Room);

        Assert.False(script.Disabled);
    }

    [Fact]
    public void ThereIsNoWayOutOfTheSandbox()
    {
        var script = Compile(@"
            function modify() {
              return {
                extraShots: (typeof System === 'undefined' ? 1 : 0)
                  + (typeof require === 'undefined' ? 1 : 0)
                  + (typeof process === 'undefined' ? 1 : 0)
                  + (typeof importNamespace === 'undefined' ? 1 : 0)
              };
            }");

        Assert.Equal(4, script.Modify(Snake, Room)!.ExtraShots);
    }

    [Fact]
    public void TheSnakeObjectIsPlainData_NotAWindowOntoTheServer()
    {
        var script = Compile(@"
            function modify(snake) {
              var reachable = 0;
              try { if (snake.GetType) reachable++; } catch (e) {}
              try { if (snake.constructor && snake.constructor.name !== 'Object') reachable++; } catch (e) {}
              return { extraShots: reachable === 0 ? 1 : 0 };
            }");

        Assert.Equal(1, script.Modify(Snake, Room)!.ExtraShots);
    }

    [Fact]
    public void Check_RunsEveryFunctionOnceWithASampleAndReportsProblems()
    {
        var good = Compile("function modify() { return {}; } function onEat() { return { heal: 1 }; }");
        var bad = Compile("function modify(snake) { return snake.nothing.deeper; }");

        good.Check();
        var failure = Assert.Throws<ScriptException>(bad.Check);

        Assert.Contains("modify", failure.Message);
    }

    [Fact]
    public void Check_ALoopThatNeverEndsIsRefused()
    {
        var script = Compile("function onEat() { for(;;) {} }");

        Assert.Throws<ScriptException>(script.Check);
    }

    [Fact]
    public void Compile_AVeryLongScriptIsRefused()
    {
        var failure = Assert.Throws<ScriptException>(() => Compile("function modify(){}" + new string(' ', PerkScript.MaxSourceLength)));

        Assert.Contains("long", failure.Message);
    }

    [Fact]
    public void State_IsKeptBetweenCalls_AndChangedInPlace()
    {
        var script = Compile("function modify(snake) { snake.state.calls = (snake.state.calls || 0) + 1; return { extraShots: snake.state.calls }; }");
        var state = new ScriptState();

        script.Modify(Snake, Room, state);
        var second = script.Modify(Snake, Room, state)!;

        Assert.Equal(2, second.ExtraShots);
        Assert.Contains("\"calls\":2", state.Json);
    }

    [Fact]
    public void State_OfOneSnakeIsNotSeenByAnother()
    {
        var script = Compile("function modify(snake) { snake.state.calls = (snake.state.calls || 0) + 1; return { extraShots: snake.state.calls }; }");
        var mine = new ScriptState();
        var yours = new ScriptState();

        script.Modify(Snake, Room, mine);
        script.Modify(Snake, Room, mine);

        Assert.Equal(1, script.Modify(Snake, Room, yours)!.ExtraShots);
    }

    [Fact]
    public void State_ThatGrowsTooBig_IsAFailureAndStaysAsItWas()
    {
        var script = Compile("function modify(snake) { snake.state.big = 'x'.repeat(3000); return {}; }");
        var state = new ScriptState();

        var result = script.Modify(Snake, Room, state);

        Assert.Null(result);
        Assert.Equal(1, script.Failures);
        Assert.Equal("{}", state.Json);
    }

    [Fact]
    public void State_ThatIsNotAnObject_StartsOver()
    {
        var script = Compile("function modify(snake) { snake.state = 5; return {}; }");
        var state = new ScriptState { Json = "{\"a\":1}" };

        script.Modify(Snake, Room, state);

        Assert.Equal("{}", state.Json);
    }

    [Fact]
    public void Modify_SeesTheClockTheAgeAndTheKills()
    {
        var script = Compile("function modify(snake, room) { return { magnetRadius: snake.ageMs / 10, regenPerSecond: snake.kills, boostSpeedBonus: room.now / 10000 }; }");
        var snake = Snake with { Now = 5_000, AgeMs = 1_230, Kills = 3 };

        var result = script.Modify(snake, Room with { Now = 5_000 })!;

        Assert.Equal(123, result.MagnetRadius);
        Assert.Equal(3, result.RegenPerSecond);
        Assert.Equal(0.5, result.BoostSpeedBonus);
    }

    [Fact]
    public void Modify_CanSayInvulnerableAndFreeShots()
    {
        var script = Compile("function modify() { return { invulnerable: true, freeShots: true }; }");

        var result = script.Modify(Snake, Room)!;

        Assert.True(result.Invulnerable);
        Assert.True(result.FreeShots);
    }

    [Fact]
    public void OnKill_GivesWhatTheScriptReturns_AndSeesTheVictim()
    {
        var script = Compile("function onKill(snake, victim, room) { return { mass: victim.length > 5 ? 10 : 0, heal: 7 }; }");

        var result = script.OnKill(Snake, new ScriptVictim("bob", 12, 30), Room)!;

        Assert.Equal(10, result.Mass);
        Assert.Equal(7, result.Heal);
    }

    [Fact]
    public void OnKill_WithoutTheFunction_GivesNothing()
        => Assert.Null(Compile("function modify() { return {}; }").OnKill(Snake, new ScriptVictim("bob", 12, 30), Room));

    [Fact]
    public void OnTick_AsksForPrizes_ByNameOrList_AtMostThreeDifferentOnes()
    {
        var one = Compile("function onTick() { return { dropPrize: 'gold' }; }");
        var many = Compile("function onTick() { return { dropPrize: ['a', 'a', 'b', 'c', 'd', 5] }; }");
        var none = Compile("function onTick() { return {}; }");

        Assert.Equal(new[] { "gold" }, one.OnTick(Room)!.DropPrizes);
        Assert.Equal(new[] { "a", "b", "c" }, many.OnTick(Room)!.DropPrizes);
        Assert.Empty(none.OnTick(Room)!.DropPrizes);
    }

    [Fact]
    public void OnTick_KeepsTheRoomsState()
    {
        var script = Compile("function onTick(room) { room.state.n = (room.state.n || 0) + 1; return room.state.n >= 2 ? { dropPrize: 'x' } : {}; }");
        var state = new ScriptState();

        var first = script.OnTick(Room, state)!;
        var second = script.OnTick(Room, state)!;

        Assert.Empty(first.DropPrizes);
        Assert.Equal(new[] { "x" }, second.DropPrizes);
    }

    [Theory]
    [InlineData("function onKill() { return {}; }")]
    [InlineData("function onTick() { return {}; }")]
    public void Compile_AScriptWithOnlyOnKillOrOnTick_IsAccepted(string source)
    {
        var script = Compile(source);

        script.Check();
    }

    [Fact]
    public void HasOnTick_IsTrueOnlyWhenTheScriptDefinesIt()
    {
        Assert.True(Compile("function onTick() { return {}; }").HasOnTick);
        Assert.False(Compile("function modify() { return {}; }").HasOnTick);
    }

    [Theory]
    [InlineData("function onKill(snake, victim) { return victim.nothing.deeper; }", "onKill")]
    [InlineData("function onTick(room) { return room.nothing.deeper; }", "onTick")]
    public void Check_RunsOnKillAndOnTickToo(string source, string name)
    {
        var failure = Assert.Throws<ScriptException>(Compile(source).Check);

        Assert.Contains(name, failure.Message);
    }
}
