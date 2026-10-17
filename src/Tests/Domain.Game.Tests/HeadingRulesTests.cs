using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class HeadingRulesTests
{
    private readonly HeadingRules _rules = new(new SnakeRulesOptions { TurnRateDegreesPerSecond = 360 });

    [Theory]
    [InlineData(0, 0)]
    [InlineData(Math.PI, Math.PI)]
    [InlineData(-Math.PI, Math.PI)]
    [InlineData(3 * Math.PI, Math.PI)]
    [InlineData(-1.5 * Math.PI, 0.5 * Math.PI)]
    [InlineData(2 * Math.PI, 0)]
    [InlineData(0.5 * Math.PI, 0.5 * Math.PI)]
    [InlineData(-0.5 * Math.PI, -0.5 * Math.PI)]
    public void Normalize_WrapsIntoMinusPiToPi(double angle, double expected)
        => Assert.Equal(expected, _rules.Normalize(angle), 9);

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void Normalize_NonFinite_Throws(double angle)
        => Assert.Throws<ArgumentOutOfRangeException>(() => _rules.Normalize(angle));

    [Fact]
    public void Turn_TargetFarAway_MovesExactlyOneStep()
        => Assert.Equal(0.25, _rules.Turn(0, 1, 0.25), 9);

    [Fact]
    public void Turn_TargetWithinOneStep_ArrivesExactly()
        => Assert.Equal(0.1, _rules.Turn(0, 0.1, 0.25));

    [Fact]
    public void Turn_TargetOnTheOtherSide_TurnsNegative()
        => Assert.Equal(-0.25, _rules.Turn(0, -1, 0.25), 9);

    [Fact]
    public void Turn_TakesTheShortWayAcrossThePiBoundary()
    {
        var current = 170 * Math.PI / 180;
        var target = -170 * Math.PI / 180;

        var turned = _rules.Turn(current, target, 0.2);

        Assert.Equal(_rules.Normalize(current + 0.2), turned, 9);
        Assert.True(turned < 0, "crossed to the negative side");
    }

    [Fact]
    public void Turn_ShortWayAcrossTheBoundary_ArrivesOnTheTarget()
    {
        var target = -170 * Math.PI / 180;

        Assert.Equal(target, _rules.Turn(170 * Math.PI / 180, target, 1), 9);
    }

    [Fact]
    public void Turn_ExactlyOpposite_TurnsPositiveSoItIsDeterministic()
        => Assert.True(_rules.Turn(0, Math.PI, 0.1) > 0);

    [Fact]
    public void Turn_ZeroStep_StaysPut()
        => Assert.Equal(0.3, _rules.Turn(0.3, 2, 0));

    [Fact]
    public void Turn_ReturnsANormalizedAngle()
        => Assert.InRange(_rules.Turn(3.0, -3.0, 0.5), -Math.PI, Math.PI);

    [Fact]
    public void Turn_ManyStepsReachTheTargetAndStopThere()
    {
        var heading = 0.0;
        foreach (var _ in Enumerable.Range(0, 100))
            heading = _rules.Turn(heading, 2.0, 0.1);

        Assert.Equal(2.0, heading);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(double.NaN)]
    public void Turn_InvalidStep_Throws(double step)
        => Assert.Throws<ArgumentOutOfRangeException>(() => _rules.Turn(0, 1, step));

    [Fact]
    public void MaxTurn_FullCirclePerSecondAtSixtyHertz_IsSixDegreesPerTick()
        => Assert.Equal(2 * Math.PI / 60, _rules.MaxTurn(TimeSpan.FromSeconds(1.0 / 60)), 5);

    [Fact]
    public void MaxTurn_ScalesWithTheTurnRate()
    {
        var slow = new HeadingRules(new SnakeRulesOptions { TurnRateDegreesPerSecond = 90 });

        Assert.Equal(Math.PI / 2 * 0.1, slow.MaxTurn(TimeSpan.FromMilliseconds(100)), 9);
    }

    [Fact]
    public void MaxTurn_NonPositiveTick_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => _rules.MaxTurn(TimeSpan.Zero));

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(Math.PI / 2, 0, 1)]
    [InlineData(-Math.PI / 2, 0, -1)]
    [InlineData(Math.PI, -1, 0)]
    public void AxisOf_IsTheUnitVectorWithYPointingDown(double angle, double x, double y)
    {
        var axis = _rules.AxisOf(angle);

        Assert.Equal(x, axis.X, 9);
        Assert.Equal(y, axis.Y, 9);
    }

    [Theory]
    [InlineData(1, 0, 0)]
    [InlineData(0, 1, Math.PI / 2)]
    [InlineData(0, -1, -Math.PI / 2)]
    [InlineData(-1, 0, Math.PI)]
    [InlineData(1, 1, Math.PI / 4)]
    public void AngleOf_IsTheInverseOfAxisOf(double x, double y, double expected)
        => Assert.Equal(expected, _rules.AngleOf(x, y), 9);

    [Fact]
    public void AngleOf_ZeroVector_IsZero()
        => Assert.Equal(0, _rules.AngleOf(0, 0));

    [Theory]
    [InlineData(0)]
    [InlineData(-90)]
    [InlineData(3601)]
    public void Constructor_InvalidTurnRate_Throws(double degreesPerSecond)
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => new HeadingRules(new SnakeRulesOptions { TurnRateDegreesPerSecond = degreesPerSecond }));
}
