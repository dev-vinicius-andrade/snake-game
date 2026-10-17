using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class BoostRulesTests
{
    private readonly BoostRules _rules = new(new SnakeRulesOptions { BoostSpeedMultiplier = 2.0, BoostTicksPerMass = 3 });

    [Fact]
    public void Step_NotRequested_IsInactiveAndResetsCounter()
    {
        var result = _rules.Step(requested: false, boostTicks: 2, mass: 10);

        Assert.False(result.Active);
        Assert.Equal(0, result.BoostTicks);
        Assert.Equal(0, result.MassSpent);
        Assert.Equal(1.0, result.SpeedMultiplier);
    }

    [Fact]
    public void Step_RequestedWithMass_IsActiveAtBoostSpeed()
    {
        var result = _rules.Step(true, 0, 10);

        Assert.True(result.Active);
        Assert.Equal(2.0, result.SpeedMultiplier);
        Assert.Equal(1, result.BoostTicks);
        Assert.Equal(0, result.MassSpent);
    }

    [Fact]
    public void Step_CounterReachesLimit_SpendsOneMassAndResets()
    {
        var result = _rules.Step(true, 2, 10);

        Assert.Equal(1, result.MassSpent);
        Assert.Equal(0, result.BoostTicks);
        Assert.True(result.Active);
    }

    [Fact]
    public void Step_NoMass_CannotBoost()
    {
        var result = _rules.Step(true, 1, 0);

        Assert.False(result.Active);
        Assert.Equal(0, result.BoostTicks);
        Assert.Equal(1.0, result.SpeedMultiplier);
    }

    [Fact]
    public void Step_HoldingLongEnough_SpendsMassEveryNthTick()
    {
        long mass = 10;
        var ticks = 0;
        var spent = 0L;
        foreach (var _ in Enumerable.Range(0, 9))
        {
            var result = _rules.Step(true, ticks, mass);
            ticks = result.BoostTicks;
            mass -= result.MassSpent;
            spent += result.MassSpent;
        }

        Assert.Equal(3, spent);
        Assert.Equal(7, mass);
    }

    [Fact]
    public void Step_LastMassSpent_DropsOutOnTheNextTick()
    {
        var last = _rules.Step(true, 2, 1);
        var next = _rules.Step(true, last.BoostTicks, 1 - last.MassSpent);

        Assert.True(last.Active);
        Assert.False(next.Active);
    }

    [Fact]
    public void Step_NegativeTicks_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => _rules.Step(true, -1, 5));

    [Theory]
    [InlineData(1.0, 3)]
    [InlineData(2.0, 0)]
    public void Constructor_InvalidOptions_Throws(double multiplier, int ticksPerMass)
        => Assert.Throws<ArgumentOutOfRangeException>(() => new BoostRules(
            new SnakeRulesOptions { BoostSpeedMultiplier = multiplier, BoostTicksPerMass = ticksPerMass }));
}
