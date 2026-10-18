using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class SnakeBodyRulesTests
{
    private const double Spacing = 10;
    private static readonly WorldBounds World = new(1000, 1000);
    private readonly SnakeBodyRules _rules = new();

    private static BodyState Body(params (double X, double Y)[] tailToHead)
        => new(tailToHead.Select(p => new Point(p.X, p.Y)).ToArray(), tailToHead.Select(_ => 1.0).ToArray());

    private static double Distance(Point a, Point b) => Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));

    [Fact]
    public void Move_Always_NewHeadIsTheLastSegment()
    {
        var moved = _rules.Move(Body((0, 0), (10, 0), (20, 0)), new Point(24, 0), 3, Spacing, World);

        Assert.Equal(new Point(24, 0), moved.Segments[^1]);
    }

    [Fact]
    public void Move_Always_FollowersAreExactlyOneSpacingBehind_WhenThereIsPathEnough()
    {
        var moved = _rules.Move(Body((0, 0), (5, 0), (10, 0)), new Point(14, 0), 3, Spacing, World);

        Assert.Equal(4, moved.Segments[1].X, 6);
        Assert.Equal(0, moved.Segments[0].X, 6);
    }

    [Fact]
    public void Move_FollowerTooFar_IsPulledToExactlySpacing()
    {
        var moved = _rules.Move(Body((0, 0), (10, 0), (20, 0)), new Point(35, 0), 3, Spacing, World);

        Assert.Equal(25, moved.Segments[1].X, 6);
        Assert.Equal(0, moved.Segments[1].Y, 6);
    }

    [Fact]
    public void Move_PullCascadesDownTheWholeBody()
    {
        var moved = _rules.Move(Body((0, 0), (10, 0), (20, 0)), new Point(35, 0), 3, Spacing, World);

        Assert.Equal(10, Distance(moved.Segments[1], moved.Segments[2]), 6);
        Assert.Equal(10, Distance(moved.Segments[0], moved.Segments[1]), 6);
    }

    [Fact]
    public void Move_TurningHead_FollowersKeepSpacingAlongTheCurve()
    {
        var moved = _rules.Move(Body((0, 0), (10, 0), (20, 0)), new Point(20, 14), 3, Spacing, World);

        Assert.All(Enumerable.Range(0, 2), i => Assert.True(Distance(moved.Segments[i], moved.Segments[i + 1]) <= Spacing + 1e-9));
    }

    [Fact]
    public void Move_HeadWrapsAroundEdge_FollowerUsesShortestWrappedDistance()
    {
        var world = new WorldBounds(100, 100);
        var body = Body((90, 50), (98, 50));

        var moved = _rules.Move(body, new Point(2, 50), 2, Spacing, world);

        Assert.Equal(92, moved.Segments[0].X, 6);
        Assert.Equal(50, moved.Segments[0].Y, 6);
    }

    [Fact]
    public void Move_TargetLonger_AddsOneFullHpSegmentAtTheTail()
    {
        var body = Body((0, 0), (10, 0)) with { Health = new[] { 0.5, 0.8 } };

        var moved = _rules.Move(body, new Point(14, 0), 10, Spacing, World);

        Assert.Equal(3, moved.Segments.Count);
        Assert.Equal(moved.Segments[1], moved.Segments[0]);
        Assert.Equal(new[] { 1.0, 0.5, 0.8 }, moved.Health);
    }

    [Fact]
    public void Move_TargetEqual_KeepsSegmentCount()
        => Assert.Equal(3, _rules.Move(Body((0, 0), (10, 0), (20, 0)), new Point(21, 0), 3, Spacing, World).Segments.Count);

    [Fact]
    public void Move_TargetShorter_TrimsTailSegmentsAndTheirHealth()
    {
        var body = Body((0, 0), (10, 0), (20, 0), (30, 0)) with { Health = new[] { 0.1, 0.2, 0.3, 0.4 } };

        var moved = _rules.Move(body, new Point(31, 0), 2, Spacing, World);

        Assert.Equal(2, moved.Segments.Count);
        Assert.Equal(new[] { 0.3, 0.4 }, moved.Health);
    }

    [Fact]
    public void Move_HealthOutOfSync_IsRepairedToMatchSegments()
    {
        var body = Body((0, 0), (10, 0), (20, 0)) with { Health = new[] { 0.5 } };

        var moved = _rules.Move(body, new Point(21, 0), 3, Spacing, World);

        Assert.Equal(new[] { 1.0, 1.0, 0.5 }, moved.Health);
    }

    [Fact]
    public void Move_EmptyBody_BecomesJustTheHead()
    {
        var moved = _rules.Move(new BodyState(Array.Empty<Point>(), Array.Empty<double>()), new Point(5, 5), 1, Spacing, World);

        Assert.Equal(new[] { new Point(5, 5) }, moved.Segments);
        Assert.Equal(new[] { 1.0 }, moved.Health);
    }

    [Fact]
    public void Move_Always_DoesNotMutateInput()
    {
        var body = Body((0, 0), (10, 0), (20, 0));

        _rules.Move(body, new Point(35, 0), 3, Spacing, World);

        Assert.Equal(new Point(10, 0), body.Segments[1]);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(3, 0)]
    public void Move_InvalidArguments_Throw(int target, double spacing)
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => _rules.Move(Body((0, 0)), new Point(1, 1), target, spacing, World));

    private static Point OnCircle(double angle, double radius) => new(500 + radius * Math.Cos(angle), 500 + radius * Math.Sin(angle));

    /// <summary>A snake of 12 segments running in a circle of the given radius, for the given number of ticks.</summary>
    private BodyState RunInCircle(double radius, double stepLength, int ticks, int length = 12, double spacing = 15)
    {
        var angleStep = stepLength / radius;
        var straight = Enumerable.Range(0, length).Select(i => new Point(500 + radius, 500 + (length - 1 - i) * spacing)).ToArray();
        var body = new BodyState(straight, straight.Select(_ => 1.0).ToArray());
        var angle = 0.0;
        for (var tick = 0; tick < ticks; tick++)
        {
            angle += angleStep;
            body = _rules.Move(body, OnCircle(angle, radius), length, spacing, World);
        }
        return body;
    }

    [Fact]
    public void Move_RunningInATightCircle_EverySegmentStaysOnTheCircle()
    {
        var body = RunInCircle(radius: 36, stepLength: 3.75, ticks: 600);

        foreach (var segment in body.Segments)
            Assert.InRange(Distance(segment, new Point(500, 500)), 36 - 1.5, 36 + 1.5);
    }

    [Fact]
    public void Move_RunningInATightCircle_TheTailKeepsMoving()
    {
        var before = RunInCircle(radius: 36, stepLength: 3.75, ticks: 600);
        var after = RunInCircle(radius: 36, stepLength: 3.75, ticks: 601);

        Assert.InRange(Distance(before.Segments[0], after.Segments[0]), 3.0, 4.5);
    }

    [Fact]
    public void Move_RunningInATightCircle_TheBodyDoesNotCollapseOntoItself()
    {
        var body = RunInCircle(radius: 36, stepLength: 3.75, ticks: 600);

        Assert.True(Distance(body.Segments[0], body.Segments[^1]) > 40);
    }

    [Fact]
    public void Move_RunningInACircleForALongTime_DoesNotDrift()
    {
        var early = RunInCircle(radius: 36, stepLength: 3.75, ticks: 300);
        var late = RunInCircle(radius: 36, stepLength: 3.75, ticks: 6000);

        Assert.InRange(Distance(late.Segments[0], new Point(500, 500)), Distance(early.Segments[0], new Point(500, 500)) - 0.5, 36 + 1.5);
    }

    [Fact]
    public void Move_RunningStraight_SegmentsAreEvenlySpacedBehindTheHead()
    {
        var body = Body((100, 500), (115, 500), (130, 500), (145, 500));
        for (var tick = 1; tick <= 100; tick++)
            body = _rules.Move(body, new Point(145 + tick * 3.75, 500), 4, 15, World);

        for (var i = 0; i < body.Segments.Count - 1; i++)
            Assert.Equal(15, Distance(body.Segments[i], body.Segments[i + 1]), 3);
    }

    [Fact]
    public void Move_KeepsATrailOfWhereTheHeadHasBeen()
    {
        var moved = _rules.Move(Body((0, 0), (10, 0)), new Point(14, 0), 2, Spacing, World);

        Assert.Equal(new Point(14, 0), moved.Trail[^1]);
        Assert.True(moved.Trail.Count >= 2);
    }

    [Fact]
    public void Move_TheTrailDoesNotGrowForever()
    {
        var body = RunInCircle(radius: 200, stepLength: 3.75, ticks: 5000);

        Assert.True(body.Trail.Count < 200);
    }

    [Fact]
    public void Move_HeadHasNotMoved_DoesNotPileUpTrailPoints()
    {
        var first = _rules.Move(Body((0, 0), (10, 0)), new Point(10, 0), 2, Spacing, World);
        var second = _rules.Move(first, new Point(10, 0), 2, Spacing, World);

        Assert.Equal(first.Trail.Count, second.Trail.Count);
    }

    [Fact]
    public void Move_NoTrailYet_StartsFromTheSegments()
    {
        var moved = _rules.Move(Body((0, 0), (10, 0), (20, 0)), new Point(30, 0), 3, Spacing, World);

        Assert.Equal(20, moved.Segments[1].X, 6);
        Assert.Equal(10, moved.Segments[0].X, 6);
    }

    [Fact]
    public void Move_NotEnoughPathYet_ExtraSegmentsStayStackedOnTheTail()
    {
        var moved = _rules.Move(Body((0, 0), (0, 0), (0, 0)), new Point(5, 0), 3, Spacing, World);

        Assert.Equal(new Point(0, 0), moved.Segments[0]);
        Assert.Equal(new Point(0, 0), moved.Segments[1]);
    }

    [Fact]
    public void Move_CrossingTheWorldEdge_KeepsTheSpacingAndStaysInsideTheWorld()
    {
        var world = new WorldBounds(100, 100);
        var body = Body((70, 50), (80, 50), (90, 50));

        for (var x = 94.0; x < 130; x += 4)
        {
            body = _rules.Move(body, new Point(x % 100, 50), 3, Spacing, world);
            Assert.All(body.Segments, p => Assert.InRange(p.X, 0, 100));
        }

        var gap = body.Segments[1].X - body.Segments[2].X;
        if (gap > 50) gap -= 100;
        if (gap < -50) gap += 100;
        Assert.Equal(10, Math.Abs(gap), 3);
    }
}
