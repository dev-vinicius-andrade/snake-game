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
    public void Move_FollowerWithinSpacing_StaysWhereItWas()
    {
        var moved = _rules.Move(Body((0, 0), (5, 0), (10, 0)), new Point(14, 0), 3, Spacing, World);

        Assert.Equal(new Point(5, 0), moved.Segments[1]);
        Assert.Equal(new Point(0, 0), moved.Segments[0]);
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
}
