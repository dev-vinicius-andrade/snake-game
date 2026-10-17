using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class SnakeSizeRulesTests
{
    private readonly SnakeSizeRules _rules = new(new SnakeRulesOptions());

    [Theory]
    [InlineData(0, 6)]
    [InlineData(1, 7)]
    [InlineData(10, 16)]
    [InlineData(-5, 6)]
    public void TargetLength_MassGiven_AddsOneSegmentPerFruit(long mass, int expected)
        => Assert.Equal(expected, _rules.TargetLength(initialLength: 6, mass));

    [Theory]
    [InlineData(4, 8)]
    [InlineData(5, 8)]
    [InlineData(6, 9)]
    public void TargetLength_MassPerSegmentTwo_NeedsTwoFruitsPerSegment(long mass, int expected)
        => Assert.Equal(expected, new SnakeSizeRules(new SnakeRulesOptions { MassPerSegment = 2 }).TargetLength(6, mass));

    [Fact]
    public void TargetLength_InitialLengthBelowOne_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => _rules.TargetLength(0, 3));

    [Fact]
    public void Constructor_InvalidMassPerSegment_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new SnakeSizeRules(new SnakeRulesOptions { MassPerSegment = 0 }));
}
