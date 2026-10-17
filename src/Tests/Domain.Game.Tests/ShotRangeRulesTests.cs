using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class ShotRangeRulesTests
{
    private const double Width = 1920;
    private const double Height = 1080;
    private readonly ShotRangeRules _rules = new();

    [Theory]
    [InlineData(1, 0, 1920, 50)]
    [InlineData(-1, 0, 0, 50)]
    [InlineData(0, 1, 100, 1080)]
    [InlineData(0, -1, 100, 0)]
    public void EndPoint_CardinalShot_EndsAtTheEdgeInFrontOfIt(double axisX, double axisY, double endX, double endY)
    {
        var end = _rules.EndPoint(new Point(100, 50), new Point(axisX, axisY), Width, Height);

        Assert.Equal(endX, end.X, 6);
        Assert.Equal(endY, end.Y, 6);
    }

    [Fact]
    public void EndPoint_DiagonalShot_EndsWhereItFirstLeavesTheRoom()
    {
        var axis = new Point(Math.Cos(Math.PI / 4), Math.Sin(Math.PI / 4));

        var end = _rules.EndPoint(new Point(0, 0), axis, Width, Height);

        Assert.Equal(1080, end.X, 6);
        Assert.Equal(1080, end.Y, 6);
    }

    [Fact]
    public void EndPoint_ShallowAngle_LeavesThroughTheSide()
    {
        var axis = new Point(Math.Cos(0.1), Math.Sin(0.1));

        var end = _rules.EndPoint(new Point(0, 0), axis, Width, Height);

        Assert.Equal(Width, end.X, 6);
        Assert.InRange(end.Y, 0, Height);
    }

    [Fact]
    public void EndPoint_OriginAlreadyOutsideInFront_EndsAtTheOrigin()
    {
        var end = _rules.EndPoint(new Point(2000, 50), new Point(1, 0), Width, Height);

        Assert.Equal(new Point(2000, 50), end);
    }

    [Fact]
    public void EndPoint_ZeroAxis_EndsAtTheOrigin()
        => Assert.Equal(new Point(10, 20), _rules.EndPoint(new Point(10, 20), new Point(0, 0), Width, Height));

    [Theory]
    [InlineData(0, 0)]
    [InlineData(-5, 100)]
    public void EndPoint_NonPositiveRoom_Throws(double width, double height)
        => Assert.Throws<ArgumentOutOfRangeException>(() => _rules.EndPoint(new Point(1, 1), new Point(1, 0), width, height));

    [Theory]
    [InlineData(100, 50, false)]
    [InlineData(1000, 50, false)]
    [InlineData(1919.9, 50, false)]
    [InlineData(1920, 50, true)]
    [InlineData(2500, 50, true)]
    public void HasReachedEnd_RightwardShot_IsTrueFromTheEdgeOn(double currentX, double currentY, bool expected)
        => Assert.Equal(expected, _rules.HasReachedEnd(new Point(100, 50), new Point(currentX, currentY), new Point(1920, 50)));

    [Fact]
    public void HasReachedEnd_DiagonalShot_UsesTheTravelledDistance()
    {
        var origin = new Point(0, 0);
        var end = new Point(1080, 1080);

        Assert.False(_rules.HasReachedEnd(origin, new Point(500, 500), end));
        Assert.True(_rules.HasReachedEnd(origin, new Point(1080, 1080), end));
    }

    [Fact]
    public void HasReachedEnd_EndEqualsOrigin_IsImmediatelyTrue()
        => Assert.True(_rules.HasReachedEnd(new Point(5, 5), new Point(5, 5), new Point(5, 5)));
}
