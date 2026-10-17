using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class ShotCooldownRulesTests
{
    private readonly ShotCooldownRules _rules = new(new SnakeRulesOptions { ShotCooldownMilliseconds = 250 });

    [Theory]
    [InlineData(1000.0 / 60, 15)]
    [InlineData(1000.0 / 30, 8)]
    [InlineData(100, 3)]
    [InlineData(250, 1)]
    [InlineData(1000, 1)]
    [InlineData(1, 250)]
    public void CooldownTicks_RoundsUpToWholeTicks(double tickMilliseconds, int expected)
        => Assert.Equal(expected, _rules.CooldownTicks(TimeSpan.FromMilliseconds(tickMilliseconds)));

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void CooldownTicks_NonPositiveTick_Throws(double tickMilliseconds)
        => Assert.Throws<ArgumentOutOfRangeException>(() => _rules.CooldownTicks(TimeSpan.FromMilliseconds(tickMilliseconds)));

    [Theory]
    [InlineData(15, 14)]
    [InlineData(1, 0)]
    [InlineData(0, 0)]
    [InlineData(-3, 0)]
    public void Tick_CountsDownAndStopsAtZero(int remaining, int expected)
        => Assert.Equal(expected, _rules.Tick(remaining));

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(1, false)]
    [InlineData(15, false)]
    public void CanShoot_OnlyWhenCooldownIsOver(int remaining, bool expected)
        => Assert.Equal(expected, _rules.CanShoot(remaining));

    [Fact]
    public void Constructor_NonPositiveCooldown_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new ShotCooldownRules(new SnakeRulesOptions { ShotCooldownMilliseconds = 0 }));
}
