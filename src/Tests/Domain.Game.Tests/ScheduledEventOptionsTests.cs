using Domain.Game.Rules;
using NSubstitute;

namespace Domain.Game.Tests;

public sealed class ScheduledEventOptionsTests
{
    private static WorldEventOptions With(Func<WorldEventOptions, WorldEventOptions> change) => change(new WorldEventOptions());

    [Fact]
    public void Defaults_AreOnAndValid()
    {
        var events = GameplayConfig.Defaults.WorldEvents;

        Assert.True(events.FruitShower.Enabled);
        Assert.True(events.Bounty.Enabled);
        Assert.True(events.Mines.Enabled);
        Assert.True(events.SugarRush.Enabled);
        Assert.Equal(20, events.FruitShower.DurationSeconds);
        Assert.Equal(30, events.SugarRush.DurationSeconds);
        GameplayConfig.Defaults.EnsureValid();
    }

    [Fact]
    public void Json_AConfigWithoutTheNewEventsGetsTheirDefaults()
    {
        const string old = "{\"worldEvents\":{\"enabled\":true,\"zone\":{\"enabled\":true}}}";

        var config = System.Text.Json.JsonSerializer.Deserialize<GameplayConfig>(old, GameplayConfig.Json)!;

        Assert.Equal(new FruitShowerOptions().FruitsPerSecond, config.WorldEvents.FruitShower.FruitsPerSecond);
        Assert.True(config.WorldEvents.FruitShower.Enabled);
        Assert.Equal(new BountyOptions().RewardMass, config.WorldEvents.Bounty.RewardMass);
        Assert.Equal(new MineFieldOptions().MaxMines, config.WorldEvents.Mines.MaxMines);
        Assert.Equal(new SugarRushOptions().DurationSeconds, config.WorldEvents.SugarRush.DurationSeconds);
        config.EnsureValid();
    }

    [Fact]
    public void Json_NewEventsRoundTrip()
    {
        var events = new WorldEventOptions
        {
            FruitShower = new FruitShowerOptions { FruitsPerSecond = 12, Rooms = ["a"] },
            Bounty = new BountyOptions { RewardMass = 300, ExcludedRooms = ["b"] },
            Mines = new MineFieldOptions { MinMines = 2, MaxMines = 3 },
            SugarRush = new SugarRushOptions { DurationSeconds = 45 }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(new GameplayConfig { WorldEvents = events }, GameplayConfig.Json);
        var back = System.Text.Json.JsonSerializer.Deserialize<GameplayConfig>(json, GameplayConfig.Json)!.WorldEvents;

        Assert.Contains("\"fruitShower\"", json);
        Assert.Contains("\"sugarRush\"", json);
        Assert.Equal(12, back.FruitShower.FruitsPerSecond);
        Assert.Equal(["a"], back.FruitShower.Rooms);
        Assert.Equal(300, back.Bounty.RewardMass);
        Assert.Equal(["b"], back.Bounty.ExcludedRooms);
        Assert.Equal((2, 3), (back.Mines.MinMines, back.Mines.MaxMines));
        Assert.Equal(45, back.SugarRush.DurationSeconds);
    }

    public static TheoryData<string, Func<WorldEventOptions, WorldEventOptions>> Refused => new()
    {
        { "shower no interval", o => o with { FruitShower = o.FruitShower with { MinIntervalSeconds = 0 } } },
        { "shower inverted interval", o => o with { FruitShower = o.FruitShower with { MinIntervalSeconds = 50, MaxIntervalSeconds = 10 } } },
        { "shower no duration", o => o with { FruitShower = o.FruitShower with { DurationSeconds = 0 } } },
        { "shower too long", o => o with { FruitShower = o.FruitShower with { DurationSeconds = 61 } } },
        { "shower no fruit", o => o with { FruitShower = o.FruitShower with { FruitsPerSecond = 0 } } },
        { "shower too much fruit", o => o with { FruitShower = o.FruitShower with { FruitsPerSecond = 31 } } },
        { "shower no players", o => o with { FruitShower = o.FruitShower with { MinPlayers = 0 } } },
        { "shower blank room", o => o with { FruitShower = o.FruitShower with { Rooms = [" "] } } },
        { "bounty no duration", o => o with { Bounty = o.Bounty with { DurationSeconds = 4 } } },
        { "bounty negative reward", o => o with { Bounty = o.Bounty with { RewardMass = -1 } } },
        { "bounty huge reward", o => o with { Bounty = o.Bounty with { RewardMass = 10001 } } },
        { "bounty foods credit", o => o with { Bounty = o.Bounty with { FoodsCredit = 1001 } } },
        { "bounty blank excluded room", o => o with { Bounty = o.Bounty with { ExcludedRooms = [""] } } },
        { "mines none", o => o with { Mines = o.Mines with { MinMines = 0 } } },
        { "mines inverted", o => o with { Mines = o.Mines with { MinMines = 9, MaxMines = 3 } } },
        { "mines too many", o => o with { Mines = o.Mines with { MaxMines = WorldEventRules.MaxActiveMines + 1 } } },
        { "mines no blast", o => o with { Mines = o.Mines with { Radius = 0 } } },
        { "mines no trigger", o => o with { Mines = o.Mines with { TriggerRadius = 0 } } },
        { "mines arm too long", o => o with { Mines = o.Mines with { ArmMilliseconds = 60001 } } },
        { "rush no duration", o => o with { SugarRush = o.SugarRush with { DurationSeconds = 0 } } },
        { "rush too long", o => o with { SugarRush = o.SugarRush with { DurationSeconds = 601 } } },
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public void Rules_RefuseOptionsThatCannotWork(string name, Func<WorldEventOptions, WorldEventOptions> change)
    {
        Assert.NotNull(name);
        Assert.ThrowsAny<ArgumentException>(() => new WorldEventRules(With(change)));
    }

    [Fact]
    public void Reconfigure_KeepsTheOldOptionsWhenTheNewOnesAreRefused()
    {
        var rules = new WorldEventRules(new WorldEventOptions());

        Assert.Throws<ArgumentOutOfRangeException>(() => rules.Reconfigure(With(o => o with { Mines = o.Mines with { MinMines = 0 } })));

        Assert.Equal(new MineFieldOptions().MinMines, rules.Options.Mines.MinMines);
    }

    [Fact]
    public void AppliesTo_FollowsTheSwitchAndTheRooms()
    {
        var room = NSubstitute.Substitute.For<Library.Commons.Game.Domain.Interfaces.Entities.IRoom>();
        room.Guid.Returns(Guid.NewGuid());
        room.Name = "arena";

        Assert.True(new BountyOptions().AppliesTo(room));
        Assert.False(new BountyOptions { Enabled = false }.AppliesTo(room));
        Assert.False(new MineFieldOptions { Rooms = ["other"] }.AppliesTo(room));
        Assert.True(new MineFieldOptions { Rooms = ["arena"] }.AppliesTo(room));
        Assert.False(new SugarRushOptions { Rooms = ["arena"], ExcludedRooms = ["arena"] }.AppliesTo(room));
        Assert.False(new FruitShowerOptions { ExcludedRooms = ["ARENA"] }.AppliesTo(room));
    }
}

public sealed class EventScheduleTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly SugarRushOptions Settings = new() { MinIntervalSeconds = 10, MaxIntervalSeconds = 10, DurationSeconds = 30, MinPlayers = 2 };

    private static EventSchedule Schedule() => new(() => 0.5);

    [Fact]
    public void Step_TheFirstLookSchedulesTheNextAndStartsNothing()
    {
        var step = Schedule().Step(Settings, true, EventWindow.None, T0, 5, 30);

        Assert.Equal(ScheduleAction.None, step.Action);
        Assert.Equal(T0.AddSeconds(10), step.State.NextAt);
    }

    [Fact]
    public void Step_WaitsUntilItIsDue()
    {
        var waiting = new EventWindow(T0.AddSeconds(10), null);

        Assert.Equal(ScheduleAction.None, Schedule().Step(Settings, true, waiting, T0.AddSeconds(9), 5, 30).Action);
    }

    [Fact]
    public void Step_WaitsForEnoughPlayers()
    {
        var due = new EventWindow(T0, null);

        var step = Schedule().Step(Settings, true, due, T0.AddSeconds(5), 1, 30);

        Assert.Equal(ScheduleAction.None, step.Action);
        Assert.Equal(due, step.State);
    }

    [Fact]
    public void Step_BeginsForTheDurationOnceDue()
    {
        var step = Schedule().Step(Settings, true, new EventWindow(T0, null), T0.AddSeconds(5), 2, 30);

        Assert.Equal(ScheduleAction.Begin, step.Action);
        Assert.True(step.State.IsActive);
        Assert.Equal(T0.AddSeconds(35), step.State.EndsAt);
    }

    [Fact]
    public void Step_WhileGoingOnDoesNothingThenEndsAndSchedulesTheNext()
    {
        var going = new EventWindow(null, T0.AddSeconds(30));

        Assert.Equal(ScheduleAction.None, Schedule().Step(Settings, true, going, T0.AddSeconds(29), 2, 30).Action);
        var end = Schedule().Step(Settings, true, going, T0.AddSeconds(30), 2, 30);
        Assert.Equal(ScheduleAction.End, end.Action);
        Assert.False(end.State.IsActive);
        Assert.Equal(T0.AddSeconds(40), end.State.NextAt);
    }

    [Fact]
    public void Step_AnInstantEventBeginsAndSchedulesTheNextAtOnce()
    {
        var step = Schedule().Step(Settings, true, new EventWindow(T0, null), T0, 2, 0);

        Assert.Equal(ScheduleAction.Begin, step.Action);
        Assert.False(step.State.IsActive);
        Assert.Equal(T0.AddSeconds(10), step.State.NextAt);
    }

    [Fact]
    public void Step_SwitchedOffForgetsEverything()
    {
        var going = new EventWindow(null, T0.AddSeconds(30));

        Assert.Equal(EventWindow.None, Schedule().Step(Settings with { Enabled = false }, true, going, T0, 2, 30).State);
        Assert.Equal(EventWindow.None, Schedule().Step(Settings, false, going, T0, 2, 30).State);
    }

    [Fact]
    public void Scheduled_DrawsEvenlyBetweenTheTwoIntervals()
    {
        var settings = Settings with { MinIntervalSeconds = 10, MaxIntervalSeconds = 30 };

        Assert.Equal(T0.AddSeconds(20), new EventSchedule(() => 0.5).Scheduled(settings, T0).NextAt);
        Assert.Equal(T0.AddSeconds(10), new EventSchedule(() => 0).Scheduled(settings, T0).NextAt);
    }
}
