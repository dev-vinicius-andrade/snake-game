using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class WorldEventRulesTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private const double Width = 1000;
    private const double Height = 500;

    private static WorldEventOptions Options(Func<LightningOptions, LightningOptions>? change = null)
    {
        var lightning = new LightningOptions { MinIntervalSeconds = 20, MaxIntervalSeconds = 60, Radius = 80, TelegraphMilliseconds = 1500 };
        return new WorldEventOptions { Lightning = change?.Invoke(lightning) ?? lightning };
    }

    private static WorldEventRules Rules(WorldEventOptions? options = null, double random = 0) => new(options ?? Options(), () => random);

    [Fact]
    public void Step_FirstLook_SchedulesTheFirstStrikeWithinTheInterval()
    {
        var step = Rules(random: 0.5).Step(LightningState.None, Start, Width, Height);

        Assert.Equal(LightningAction.None, step.Action);
        Assert.Equal(Start.AddSeconds(40), step.State.NextAt);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 60)]
    public void Step_TheIntervalIsDrawnBetweenMinAndMax(double random, int seconds)
    {
        var step = Rules(random: random).Step(LightningState.None, Start, Width, Height);

        Assert.Equal(Start.AddSeconds(seconds), step.State.NextAt);
    }

    [Fact]
    public void Step_BeforeItIsDue_DoesNothing()
    {
        var state = new LightningState(Start.AddSeconds(30), null);

        var step = Rules().Step(state, Start.AddSeconds(29), Width, Height);

        Assert.Equal(LightningAction.None, step.Action);
        Assert.Equal(state, step.State);
    }

    [Fact]
    public void Step_WhenDue_TelegraphsASpotInsideTheRoom()
    {
        var state = new LightningState(Start.AddSeconds(30), null);

        var step = Rules(random: 0.25).Step(state, Start.AddSeconds(30), Width, Height);

        Assert.Equal(LightningAction.Telegraph, step.Action);
        var pending = step.State.Pending!.Value;
        Assert.Equal(250, pending.X);
        Assert.Equal(125, pending.Y);
        Assert.Equal(80, pending.Radius);
        Assert.Equal(Start.AddSeconds(31.5), pending.StrikesAt);
        Assert.Null(step.State.NextAt);
        Assert.Equal(pending, step.Strike);
    }

    [Fact]
    public void Step_WhileTheWarningRuns_DoesNothing()
    {
        var pending = new LightningStrike(10, 20, 80, Start.AddSeconds(31.5));
        var state = new LightningState(null, pending);

        var step = Rules().Step(state, Start.AddSeconds(31), Width, Height);

        Assert.Equal(LightningAction.None, step.Action);
        Assert.Equal(state, step.State);
    }

    [Fact]
    public void Step_WhenTheWarningRunsOut_StrikesAndSchedulesTheNext()
    {
        var pending = new LightningStrike(10, 20, 80, Start.AddSeconds(31.5));

        var step = Rules(random: 0).Step(new LightningState(null, pending), Start.AddSeconds(31.5), Width, Height);

        Assert.Equal(LightningAction.Strike, step.Action);
        Assert.Equal(pending, step.Strike);
        Assert.Null(step.State.Pending);
        Assert.Equal(Start.AddSeconds(51.5), step.State.NextAt);
    }

    [Fact]
    public void Step_LateAfterTheWarning_StillStrikesOnce()
    {
        var pending = new LightningStrike(10, 20, 80, Start.AddSeconds(31.5));

        var step = Rules().Step(new LightningState(null, pending), Start.AddSeconds(100), Width, Height);

        Assert.Equal(LightningAction.Strike, step.Action);
    }

    [Fact]
    public void Step_EventsOff_ForgetsEverythingAndDoesNothing()
    {
        var rules = Rules(Options() with { Enabled = false });
        var state = new LightningState(Start, new LightningStrike(1, 1, 80, Start));

        var step = rules.Step(state, Start.AddSeconds(10), Width, Height);

        Assert.Equal(LightningAction.None, step.Action);
        Assert.Equal(LightningState.None, step.State);
    }

    [Fact]
    public void Step_LightningOff_ForgetsEverythingAndDoesNothing()
    {
        var rules = Rules(Options(lightning => lightning with { Enabled = false }));

        var step = rules.Step(new LightningState(Start, null), Start.AddSeconds(10), Width, Height);

        Assert.Equal(LightningAction.None, step.Action);
        Assert.Equal(LightningState.None, step.State);
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
    [InlineData(0, 10, 50, 1500)]
    [InlineData(10, 5, 50, 1500)]
    [InlineData(5, 10, 0, 1500)]
    [InlineData(5, 10, 5000, 1500)]
    [InlineData(5, 10, 50, 50)]
    [InlineData(5, 10, 50, 60000)]
    public void Constructor_OptionsOutOfRange_AreRefused(int min, int max, int radius, int telegraph)
    {
        var options = Options(lightning => lightning with
        {
            MinIntervalSeconds = min, MaxIntervalSeconds = max, Radius = radius, TelegraphMilliseconds = telegraph
        });

        Assert.Throws<ArgumentOutOfRangeException>(() => new WorldEventRules(options));
    }

    [Fact]
    public void Constructor_ABlankRoom_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => new WorldEventRules(Options(lightning => lightning with { Rooms = [" "] })));
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
