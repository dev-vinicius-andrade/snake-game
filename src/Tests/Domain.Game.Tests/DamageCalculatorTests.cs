using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class DamageCalculatorTests
{
    private readonly DamageCalculator _calculator = new(new DamageModelOptions());

    [Theory]
    [InlineData(5, 100)]
    [InlineData(3, 100)]
    [InlineData(10, 169.31)]
    [InlineData(40, 307.94)]
    [InlineData(80, 377.26)]
    public void MaxSegmentHp_LengthGiven_ScalesLogarithmically(int length, double expected)
        => Assert.Equal(expected, _calculator.MaxSegmentHp(length), 2);

    [Theory]
    [InlineData(10, 10, 40)]
    [InlineData(40, 40, 40)]
    [InlineData(5, 40, 14.14)]
    [InlineData(20, 80, 20)]
    [InlineData(80, 20, 80)]
    public void ShotDamage_SizesGiven_FollowsSizeRatio(int shooter, int target, double expected)
        => Assert.Equal(expected, _calculator.ShotDamage(shooter, target), 2);

    [Fact]
    public void ShotDamage_HugeShooterVsTiny_CappedAtHalfMaxHp()
        => Assert.Equal(50, _calculator.ShotDamage(40, 5), 6);

    [Fact]
    public void ShotDamage_TinyShooterVsHuge_NotBelowMinimum()
        => Assert.Equal(5, _calculator.ShotDamage(1, 10_000), 6);

    [Theory]
    [InlineData(10, 10, 5)]
    [InlineData(40, 40, 8)]
    [InlineData(5, 40, 22)]
    [InlineData(20, 80, 19)]
    [InlineData(80, 20, 3)]
    [InlineData(40, 5, 2)]
    public void ShotsToBreak_DesignTable_MatchesDocument(int shooter, int target, int expected)
        => Assert.Equal(expected, _calculator.ShotsToBreak(shooter, target));

    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 5)]
    [InlineData(500, 5)]
    [InlineData(5, 500)]
    [InlineData(300, 300)]
    [InlineData(1000, 3)]
    public void ShotsToBreak_AnySizes_NeverOneShot(int shooter, int target)
        => Assert.True(_calculator.ShotsToBreak(shooter, target) >= 2);

    [Fact]
    public void HealAmount_FruitValueGiven_MultipliesHealPerFruit()
        => Assert.Equal(60, _calculator.HealAmount(2));

    [Fact]
    public void HealAmount_NegativeValue_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.HealAmount(-1));

    [Theory]
    [InlineData(0, 5)]
    [InlineData(5, 0)]
    [InlineData(-1, -1)]
    public void ShotDamage_InvalidLength_Throws(int shooter, int target)
        => Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.ShotDamage(shooter, target));

    [Fact]
    public void Constructor_InvalidOptions_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => new DamageCalculator(new DamageModelOptions { MaxShotDamageFraction = 1.5 }));

    [Fact]
    public void Constructor_NullOptions_Throws()
        => Assert.Throws<ArgumentNullException>(() => new DamageCalculator(null!));
}
