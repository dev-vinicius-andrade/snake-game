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
    public void Advance_TargetLonger_GrowsByOneSegmentPerStep()
    {
        var path = new[] { 1, 2, 3 };

        var next = _rules.Advance(path, 4, targetLength: 10);

        Assert.Equal(new[] { 1, 2, 3, 4 }, next);
    }

    [Fact]
    public void Advance_TargetEqual_KeepsLengthAndDropsTail()
    {
        var next = _rules.Advance(new[] { 1, 2, 3 }, 4, targetLength: 3);

        Assert.Equal(new[] { 2, 3, 4 }, next);
    }

    [Fact]
    public void Advance_TargetShorter_TrimsTailToTarget()
    {
        var next = _rules.Advance(new[] { 1, 2, 3, 4, 5 }, 6, targetLength: 2);

        Assert.Equal(new[] { 5, 6 }, next);
    }

    [Fact]
    public void Advance_Always_PutsNewHeadLast()
        => Assert.Equal(9, _rules.Advance(new[] { 1, 2 }, 9, 5).Last());

    [Fact]
    public void Advance_Always_DoesNotMutateInput()
    {
        var path = new List<int> { 1, 2, 3 };

        _rules.Advance(path, 4, 3);

        Assert.Equal(new[] { 1, 2, 3 }, path);
    }

    [Fact]
    public void Advance_TargetBelowOne_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => _rules.Advance(new[] { 1 }, 2, 0));

    [Fact]
    public void Constructor_InvalidMassPerSegment_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new SnakeSizeRules(new SnakeRulesOptions { MassPerSegment = 0 }));
}
