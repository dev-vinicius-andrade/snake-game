using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class WorldEventRulesTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private const double Width = 1000;
    private const double Height = 500;
    private static readonly IReadOnlyList<Point> NoHeads = [];

    private static WorldEventOptions Options(Func<LightningOptions, LightningOptions>? change = null)
    {
        var lightning = new LightningOptions
        {
            MinIntervalSeconds = 20, MaxIntervalSeconds = 60, Radius = 80, TelegraphMilliseconds = 1500,
            MinStrikes = 2, MaxStrikes = 6, StormSeconds = 6
        };
        return new WorldEventOptions { Lightning = change?.Invoke(lightning) ?? lightning };
    }

    private static LightningOptions OneStrike(LightningOptions lightning) => lightning with { MinStrikes = 1, MaxStrikes = 1, StormSeconds = 1 };

    private static WorldEventRules Rules(WorldEventOptions? options = null, double random = 0) => new(options ?? Options(), () => random);

    private static StormStep Step(WorldEventRules rules, StormState state, double seconds, IReadOnlyList<Point>? heads = null, WorldEventKind kind = WorldEventKind.Lightning)
        => rules.Step(kind, state, Start.AddSeconds(seconds), Width, Height, heads ?? NoHeads);

    /// <summary>Begins a storm at 30 s and looks again at 31 s, so strikes whose moment is drawn (up to a second in) are marked.</summary>
    private static StormStep Stormed(WorldEventRules rules, IReadOnlyList<Point>? heads = null)
        => Step(rules, Step(rules, Due(), 30, heads).State, 31, heads);

    private static StormState Due(double nextAtSeconds = 30) => new(Start.AddSeconds(nextAtSeconds), [], []);

    [Fact]
    public void Step_FirstLook_SchedulesTheFirstStormWithinTheInterval()
    {
        var step = Step(Rules(random: 0.5), StormState.None, 0);

        Assert.Equal(Start.AddSeconds(40), step.State.NextAt);
        Assert.Empty(step.Started);
        Assert.Empty(step.Fired);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 60)]
    public void Step_TheIntervalIsDrawnBetweenMinAndMax(double random, int seconds)
    {
        var step = Step(Rules(random: random), StormState.None, 0);

        Assert.Equal(Start.AddSeconds(seconds), step.State.NextAt);
    }

    [Fact]
    public void Step_BeforeItIsDue_DoesNothing()
    {
        var state = Due();

        var step = Step(Rules(), state, 29);

        Assert.Same(state, step.State);
        Assert.Empty(step.Started);
    }

    [Fact]
    public void Step_WhenDue_BeginsAStormWhoseStrikesAreSpreadOverItsDuration()
    {
        // Every draw is 0.5: the count is 2 + floor(0.5 * 5) = 4, and each strike begins 3 s into the 6 s storm.
        var step = Step(Rules(random: 0.5), Due(), 30);

        Assert.Empty(step.Started);
        Assert.Equal(4, step.State.Waiting.Count);
        Assert.All(step.State.Waiting, moment => Assert.Equal(Start.AddSeconds(33), moment));
        Assert.Null(step.State.NextAt);
    }

    [Theory]
    [InlineData(0, 2)]
    [InlineData(0.999, 6)]
    public void Step_TheNumberOfStrikesIsDrawnBetweenMinAndMaxInclusive(double random, int count)
    {
        var step = Step(Rules(random: random), Due(), 30);

        Assert.Equal(count, step.State.Waiting.Count + step.Started.Count);
    }

    [Fact]
    public void Step_AStrikeIsMarkedAtItsMomentWithItsOwnWarningTime()
    {
        var rules = Rules(Options(lightning => lightning with { MinStrikes = 1, MaxStrikes = 1 }), random: 0.25);
        var begun = Step(rules, Due(), 30);

        // random 0.25: one strike, beginning 1.5 s into the storm, so not yet.
        Assert.Empty(begun.Started);
        var later = Step(rules, begun.State, 31.5);

        var strike = Assert.Single(later.Started);
        Assert.Equal((250.0, 125.0, 80.0), (strike.X, strike.Y, strike.Radius));
        Assert.Equal(Start.AddSeconds(33), strike.StrikesAt);
    }

    [Fact]
    public void Step_StrikesThatBeginTogetherEachGetTheirOwnSpot()
    {
        var draws = new Queue<double>([0, 0, 0, 0.1, 0.9, 0.5, 0.5]);
        var rules = new WorldEventRules(
            Options(lightning => lightning with { MinStrikes = 2, MaxStrikes = 2, StormSeconds = 1 }), () => draws.Count > 0 ? draws.Dequeue() : 0);

        var step = rules.Step(WorldEventKind.Lightning, Due(), Start.AddSeconds(30), Width, Height, NoHeads);

        Assert.Equal(2, step.Started.Count);
        Assert.NotEqual((step.Started[0].X, step.Started[0].Y), (step.Started[1].X, step.Started[1].Y));
    }

    [Fact]
    public void Step_AStrikeFallsWhenItsOwnWarningRunsOut_AndTheOthersKeepWaiting()
    {
        var early = new StormStrike(10, 20, 80, Start.AddSeconds(31.5));
        var late = new StormStrike(30, 40, 80, Start.AddSeconds(32.5));
        var state = new StormState(null, [Start.AddSeconds(40)], [early, late]);

        var step = Step(Rules(), state, 31.5);

        Assert.Equal([early], step.Fired);
        Assert.Equal([late], step.State.Pending);
        Assert.Single(step.State.Waiting);
        Assert.Empty(step.Started);
    }

    [Fact]
    public void Step_StrikesBeginAtTheirMomentsOneByOne()
    {
        var state = new StormState(null, [Start.AddSeconds(31), Start.AddSeconds(33)], []);
        var rules = Rules(random: 0.5);

        var first = Step(rules, state, 31);
        var second = Step(rules, first.State, 32);
        var third = Step(rules, second.State, 33);

        Assert.Single(first.Started);
        Assert.Empty(second.Started);
        Assert.Single(third.Started);
        Assert.Single(third.Fired);
        Assert.Single(third.State.Pending);
    }

    [Fact]
    public void Step_WhenTheLastStrikeFalls_TheStormIsOverAndTheNextIsScheduled()
    {
        var last = new StormStrike(10, 20, 80, Start.AddSeconds(31.5));

        var step = Step(Rules(random: 0), new StormState(null, [], [last]), 31.5);

        Assert.Equal([last], step.Fired);
        Assert.False(step.State.InProgress);
        Assert.Equal(Start.AddSeconds(51.5), step.State.NextAt);
    }

    [Fact]
    public void Step_LateAfterTheMoments_MarksTheStrikeWithAFullWarningFromNow()
    {
        var state = new StormState(null, [Start.AddSeconds(31)], []);

        var step = Step(Rules(), state, 100);

        Assert.Equal(Start.AddSeconds(101.5), Assert.Single(step.Started).StrikesAt);
    }

    [Fact]
    public void Step_NeverHasMoreStrikesInFlightThanTheStormHas()
    {
        var rules = Rules(random: 0.999);
        var state = Due();
        var most = 0;
        for (var seconds = 30.0; seconds < 60; seconds += 0.05)
        {
            state = Step(rules, state, seconds).State;
            most = Math.Max(most, state.Waiting.Count + state.Pending.Count);
        }

        Assert.InRange(most, 1, 6);
    }

    [Fact]
    public void Step_AimedStrikes_LandOnALivingHeadWhenThereIsNoSpread()
    {
        var rules = Rules(Options(lightning => OneStrike(lightning) with { AimedShare = 1 }), random: 0);

        var step = Step(rules, Due(), 30, [new Point(700, 300)]);

        var strike = Assert.Single(step.Started);
        Assert.Equal((700.0, 300.0), (strike.X, strike.Y));
    }

    [Fact]
    public void Step_AimedStrikes_StayWithinAShareOfTheRadiusOfTheHead()
    {
        var rules = Rules(Options(lightning => lightning with { AimedShare = 1, MinStrikes = 40, MaxStrikes = 40, StormSeconds = 1 }), random: 0.7);

        var step = Stormed(rules, [new Point(500, 250)]);

        Assert.NotEmpty(step.Started);
        Assert.All(step.Started, strike => Assert.InRange(Math.Sqrt(Math.Pow(strike.X - 500, 2) + Math.Pow(strike.Y - 250, 2)), 0, 80 * 0.6 + 0.001));
    }

    [Fact]
    public void Step_AimedStrikes_StayInsideTheRoom()
    {
        var rules = Rules(Options(lightning => OneStrike(lightning) with { AimedShare = 1 }), random: 0.5);

        var step = Stormed(rules, [new Point(2, 1)]);

        var strike = Assert.Single(step.Started);
        Assert.InRange(strike.X, 0, Width);
        Assert.InRange(strike.Y, 0, Height);
    }

    [Fact]
    public void Step_NoShareAimed_IgnoresTheHeads()
    {
        var rules = Rules(Options(lightning => OneStrike(lightning) with { AimedShare = 0 }), random: 0.25);

        var step = Stormed(rules, [new Point(700, 300)]);

        var strike = Assert.Single(step.Started);
        Assert.Equal((250.0, 125.0), (strike.X, strike.Y));
    }

    [Fact]
    public void Step_AimedWithNobodyAlive_FallsBackToARandomSpot()
    {
        var rules = Rules(Options(lightning => OneStrike(lightning) with { AimedShare = 1 }), random: 0.25);

        var step = Stormed(rules);

        var strike = Assert.Single(step.Started);
        Assert.Equal((250.0, 125.0), (strike.X, strike.Y));
    }

    [Fact]
    public void Step_MeteorsUseTheirOwnSettings()
    {
        var options = Options() with
        {
            Meteors = new MeteorOptions { MinMeteors = 1, MaxMeteors = 1, RainSeconds = 1, Radius = 130, TelegraphMilliseconds = 2000, MinIntervalSeconds = 100, MaxIntervalSeconds = 100 }
        };
        var rules = Rules(options, random: 0);

        var first = Step(rules, StormState.None, 0, kind: WorldEventKind.Meteor);
        var started = Step(rules, new StormState(Start.AddSeconds(100), [], []), 100, kind: WorldEventKind.Meteor);

        Assert.Equal(Start.AddSeconds(100), first.State.NextAt);
        var meteor = Assert.Single(started.Started);
        Assert.Equal(130.0, meteor.Radius);
        Assert.Equal(Start.AddSeconds(102), meteor.StrikesAt);
    }

    [Fact]
    public void Step_ALightningConfigFromBeforeStorms_IsAStormOfOneStrike()
    {
        var old = new WorldEventOptions { Lightning = new LightningOptions { MinIntervalSeconds = 20, MaxIntervalSeconds = 60 } };

        var step = Step(Rules(old, random: 0.9), Due(), 30);

        Assert.Equal(1, step.State.Waiting.Count + step.Started.Count);
    }

    [Fact]
    public void WithStormDefaults_FillsInTheStormOfAnOlderConfigAndLeavesOneThatIsSet()
    {
        var filled = new WorldEventOptions().WithStormDefaults().Lightning;
        var kept = new WorldEventOptions { Lightning = new LightningOptions { MinStrikes = 3, MaxStrikes = 4, StormSeconds = 9 } }.WithStormDefaults().Lightning;

        Assert.Equal((5, 12, 6), (filled.MinStrikes, filled.MaxStrikes, filled.StormSeconds));
        Assert.Equal((3, 4, 9), (kept.MinStrikes, kept.MaxStrikes, kept.StormSeconds));
    }

    [Theory]
    [InlineData(WorldEventKind.Lightning)]
    [InlineData(WorldEventKind.Meteor)]
    public void Step_EventsOff_ForgetsEverythingAndDoesNothing(WorldEventKind kind)
    {
        var rules = Rules(Options() with { Enabled = false });
        var state = new StormState(Start, [Start], [new StormStrike(1, 1, 80, Start)]);

        var step = Step(rules, state, 10, kind: kind);

        Assert.Same(StormState.None, step.State);
        Assert.Empty(step.Started);
        Assert.Empty(step.Fired);
    }

    [Fact]
    public void Step_OneEventOff_ForgetsItButNotTheOther()
    {
        var rules = Rules(Options(lightning => lightning with { Enabled = false }));

        Assert.Same(StormState.None, Step(rules, Due(), 30).State);
        Assert.NotSame(StormState.None, Step(rules, Due(), 30, kind: WorldEventKind.Meteor).State);
    }

    [Fact]
    public void Reconfigure_SwitchesTheRulesWhileTheGameRuns()
    {
        var rules = Rules();

        rules.Reconfigure(Options(lightning => lightning with { Radius = 200 }));

        Assert.Equal(200, rules.Options.Lightning.Radius);
    }

    [Fact]
    public void Reconfigure_OptionsThatDoNotHoldUp_AreRefusedAndTheOldOnesStay()
    {
        var rules = Rules();

        Assert.Throws<ArgumentOutOfRangeException>(() => rules.Reconfigure(Options(lightning => lightning with { Radius = 0 })));

        Assert.Equal(80, rules.Options.Lightning.Radius);
    }

    [Theory]
    [InlineData(0, 10, 50, 1500, 1, 5, 5)]
    [InlineData(10, 5, 50, 1500, 1, 5, 5)]
    [InlineData(5, 10, 0, 1500, 1, 5, 5)]
    [InlineData(5, 10, 5000, 1500, 1, 5, 5)]
    [InlineData(5, 10, 50, 50, 1, 5, 5)]
    [InlineData(5, 10, 50, 60000, 1, 5, 5)]
    [InlineData(5, 10, 50, 1500, 0, 5, 5)]
    [InlineData(5, 10, 50, 1500, 6, 5, 5)]
    [InlineData(5, 10, 50, 1500, 1, 41, 5)]
    [InlineData(5, 10, 50, 1500, 1, 5, 0)]
    [InlineData(5, 10, 50, 1500, 1, 5, 500)]
    public void Constructor_LightningOutOfRange_IsRefused(int min, int max, int radius, int telegraph, int minStrikes, int maxStrikes, int stormSeconds)
    {
        var options = Options(lightning => lightning with
        {
            MinIntervalSeconds = min, MaxIntervalSeconds = max, Radius = radius, TelegraphMilliseconds = telegraph,
            MinStrikes = minStrikes, MaxStrikes = maxStrikes, StormSeconds = stormSeconds
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldEventRules(options));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(double.NaN)]
    public void Constructor_AnAimedShareOutsideZeroToOne_IsRefused(double share)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldEventRules(Options(lightning => lightning with { AimedShare = share })));
    }

    [Fact]
    public void Constructor_MeteorsOutOfRange_AreRefused()
    {
        var options = new WorldEventOptions { Meteors = new MeteorOptions { MinMeteors = 10, MaxMeteors = 5 } };

        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldEventRules(options));
    }

    [Fact]
    public void Constructor_ABlankRoom_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => new WorldEventRules(Options(lightning => lightning with { Rooms = [" "] })));
        Assert.Throws<ArgumentException>(() => new WorldEventRules(new WorldEventOptions { Meteors = new MeteorOptions { ExcludedRooms = [""] } }));
    }

    // Resolve: where a strike lands. A segment is a square of head size anchored at its point, so its centre counts.

    private const double HeadSize = 20;

    private static Point[] Body(params (double X, double Y)[] points) => points.Select(point => new Point(point.X, point.Y)).ToArray();

    [Fact]
    public void Resolve_SegmentsInsideTheRadius_AreHitAndTheOthersAreNot()
    {
        var body = Body((0, 0), (100, 0), (200, 0), (300, 0));

        var hit = WorldEventRules.Resolve(body, new Point(110, 10), 30, HeadSize, minLength: 1);

        Assert.Equal([1], hit.BrokenSegments);
        Assert.False(hit.HeadHit);
    }

    [Fact]
    public void Resolve_TheHeadInsideTheRadius_IsHit()
    {
        var body = Body((0, 0), (100, 0), (200, 0));

        var hit = WorldEventRules.Resolve(body, new Point(205, 5), 30, HeadSize, minLength: 1);

        Assert.True(hit.HeadHit);
    }

    [Fact]
    public void Resolve_TheEdgeOfTheRadius_Counts()
    {
        var body = Body((0, 0), (100, 0), (200, 0));

        var hit = WorldEventRules.Resolve(body, new Point(110 + 40, 10), 40, HeadSize, minLength: 1);

        Assert.Equal([1], hit.BrokenSegments);
    }

    [Fact]
    public void Resolve_NothingNear_HitsNothing()
    {
        var hit = WorldEventRules.Resolve(Body((0, 0), (100, 0), (200, 0)), new Point(900, 900), 50, HeadSize, minLength: 1);

        Assert.False(hit.HeadHit);
        Assert.Empty(hit.BrokenSegments);
    }

    [Fact]
    public void Resolve_NeverBreaksTheBodyBelowTheMinimumLength_KeepingTheFarthestSegments()
    {
        var body = Body((0, 0), (20, 0), (40, 0), (60, 0), (80, 0), (100, 0));

        var hit = WorldEventRules.Resolve(body, new Point(35, 10), 1000, HeadSize, minLength: 4);

        Assert.Equal(2, hit.BrokenSegments.Count);
        Assert.Equal([1, 2], hit.BrokenSegments.OrderBy(index => index));
    }

    [Fact]
    public void Resolve_NoSegmentsAtAll_HitsNothing()
    {
        var hit = WorldEventRules.Resolve([], new Point(0, 0), 50, HeadSize, minLength: 1);

        Assert.False(hit.HeadHit);
        Assert.Empty(hit.BrokenSegments);
    }
}
