using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class BodyShotRulesTests
{
    private const double HitSize = 10;
    private readonly BodyShotRules _rules = new();

    private static IReadOnlyList<Point> Line(int length)
        => Enumerable.Range(0, length).Select(i => new Point(i * 100, 0)).ToArray();

    private static BodyState Body(int length, params double[] health)
        => new(Line(length), health.Length == length ? health : Enumerable.Repeat(1.0, length).ToArray());

    [Fact]
    public void FindHit_ShotOnHead_ReportsHead()
    {
        var hit = _rules.FindHit(Line(4), new Point(305, 0), HitSize);

        Assert.Equal(new ShotHit(3, true), hit);
    }

    [Fact]
    public void FindHit_ShotOnBody_ReportsThatSegment()
    {
        var hit = _rules.FindHit(Line(4), new Point(105, 0), HitSize);

        Assert.Equal(new ShotHit(1, false), hit);
    }

    [Fact]
    public void FindHit_ShotOverlapsHeadAndBody_HeadWins()
    {
        var packed = new[] { new Point(0, 0), new Point(5, 0), new Point(8, 0) };

        Assert.True(_rules.FindHit(packed, new Point(6, 0), HitSize)!.Value.IsHead);
    }

    [Fact]
    public void FindHit_ShotOverlapsSeveralBodySegments_NearestToHeadWins()
    {
        var packed = new[] { new Point(0, 0), new Point(4, 0), new Point(8, 0), new Point(500, 0) };

        Assert.Equal(2, _rules.FindHit(packed, new Point(5, 0), HitSize)!.Value.SegmentIndex);
    }

    [Fact]
    public void FindHit_ShotMisses_ReturnsNull()
        => Assert.Null(_rules.FindHit(Line(4), new Point(50, 50), HitSize));

    [Fact]
    public void FindHit_NoSegments_ReturnsNull()
        => Assert.Null(_rules.FindHit(Array.Empty<Point>(), new Point(0, 0), HitSize));

    [Fact]
    public void ApplyDamage_PartialDamage_LowersOnlyThatSegmentRatio()
    {
        var outcome = _rules.ApplyDamage(Body(4), 1, damage: 25, maxHp: 100, canBreak: true);

        Assert.Equal(new[] { 1.0, 0.75, 1.0, 1.0 }, outcome.Body.Health);
        Assert.False(outcome.SegmentBroken);
    }

    [Fact]
    public void ApplyDamage_AlreadyDamaged_StacksOnCurrentHp()
    {
        var outcome = _rules.ApplyDamage(Body(3, 1.0, 0.5, 1.0), 1, 25, 100, true);

        Assert.Equal(0.25, outcome.Body.Health[1], 6);
    }

    [Fact]
    public void ApplyDamage_Lethal_RemovesTheSegmentAndReportsWhere()
    {
        var outcome = _rules.ApplyDamage(Body(4, 1.0, 0.2, 1.0, 1.0), 1, 25, 100, canBreak: true);

        Assert.True(outcome.SegmentBroken);
        Assert.Equal(new Point(100, 0), outcome.BrokenAt);
        Assert.Equal(new[] { new Point(0, 0), new Point(200, 0), new Point(300, 0) }, outcome.Body.Segments);
        Assert.Equal(new[] { 1.0, 1.0, 1.0 }, outcome.Body.Health);
    }

    [Fact]
    public void ApplyDamage_LethalButCannotBreak_LeavesSegmentAtZeroHp()
    {
        var outcome = _rules.ApplyDamage(Body(3), 0, 500, 100, canBreak: false);

        Assert.False(outcome.SegmentBroken);
        Assert.Equal(3, outcome.Body.Segments.Count);
        Assert.Equal(0, outcome.Body.Health[0]);
    }

    [Fact]
    public void ApplyDamage_RatioIsScaledByMaxHp()
    {
        var outcome = _rules.ApplyDamage(Body(3), 0, 50, 200, true);

        Assert.Equal(0.75, outcome.Body.Health[0], 6);
    }

    [Fact]
    public void ApplyDamage_Always_DoesNotMutateInput()
    {
        var body = Body(3);

        _rules.ApplyDamage(body, 0, 25, 100, true);

        Assert.Equal(1.0, body.Health[0]);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ApplyDamage_HeadOrOutOfRangeIndex_Throws(int index)
        => Assert.Throws<ArgumentOutOfRangeException>(() => _rules.ApplyDamage(Body(3), index, 10, 100, true));

    [Fact]
    public void ApplyDamage_NegativeDamage_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => _rules.ApplyDamage(Body(3), 0, -1, 100, true));
}
