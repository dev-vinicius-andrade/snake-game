using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class SnakeCollisionRulesTests
{
    private const double HitSize = 10;
    private readonly SnakeCollisionRules _rules = new();

    private static SnakeBody Snake(Guid id, params (double X, double Y)[] segmentsTailToHead)
        => new(id, segmentsTailToHead.Select(point => new Point(point.X, point.Y)).ToArray());

    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    [Fact]
    public void FindCollided_HeadInsideOtherBody_HeadOwnerDies()
    {
        var snakeA = Snake(A, (100, 100), (105, 100), (110, 100));
        var snakeB = Snake(B, (200, 52), (200, 100), (200, 148));
        var intruder = Snake(C, (400, 400), (205, 100));

        var collided = _rules.FindCollided(new[] { snakeA, snakeB, intruder }, HitSize);

        Assert.Equal(new[] { C }, collided);
    }

    [Fact]
    public void FindCollided_HeadInsideOwnBody_IsIgnored()
    {
        var looped = Snake(A, (100, 100), (150, 100), (101, 101));

        Assert.Empty(_rules.FindCollided(new[] { looped }, HitSize));
    }

    [Fact]
    public void FindCollided_HeadOnHead_BothDie()
    {
        var snakeA = Snake(A, (0, 0), (300, 300));
        var snakeB = Snake(B, (500, 500), (305, 305));

        var collided = _rules.FindCollided(new[] { snakeA, snakeB }, HitSize);

        Assert.Equivalent(new[] { A, B }, collided);
    }

    [Fact]
    public void FindCollided_BodiesCrossButHeadsFree_NobodyDies()
    {
        var snakeA = Snake(A, (0, 100), (100, 100), (900, 900));
        var snakeB = Snake(B, (100, 0), (100, 100), (800, 800));

        Assert.Empty(_rules.FindCollided(new[] { snakeA, snakeB }, HitSize));
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(10.01, false)]
    [InlineData(-10, true)]
    [InlineData(-10.01, false)]
    public void FindCollided_EdgeDistance_TouchingCounts(double offset, bool expectedDeath)
    {
        var wall = Snake(B, (500, 500), (500, 500));
        var runner = Snake(A, (0, 0), (500 + offset, 500));

        var collided = _rules.FindCollided(new[] { wall, runner }, HitSize);

        Assert.Equal(expectedDeath, collided.Contains(A));
    }

    [Fact]
    public void FindCollided_ThreeSnakes_OnlyTheOneThatHitDies()
    {
        var snakeA = Snake(A, (100, 100), (110, 100), (120, 100));
        var snakeB = Snake(B, (600, 600), (700, 700));
        var snakeC = Snake(C, (0, 0), (115, 100));

        var collided = _rules.FindCollided(new[] { snakeA, snakeB, snakeC }, HitSize);

        Assert.Equivalent(new[] { A, C }, collided);
    }

    [Fact]
    public void FindCollided_SingleSnakeOrNone_Empty()
    {
        Assert.Empty(_rules.FindCollided(Array.Empty<SnakeBody>(), HitSize));
        Assert.Empty(_rules.FindCollided(new[] { Snake(A, (1, 1)) }, HitSize));
    }

    [Fact]
    public void FindCollided_SnakeWithoutSegments_IsIgnored()
    {
        var empty = Snake(A);
        var other = Snake(B, (1, 1), (2, 2));

        Assert.Empty(_rules.FindCollided(new[] { empty, other }, HitSize));
    }

    [Fact]
    public void FindCollided_ListOrderDoesNotChangeOutcome()
    {
        var snakeA = Snake(A, (100, 100), (110, 100));
        var snakeB = Snake(B, (0, 0), (105, 100));

        var forward = _rules.FindCollided(new[] { snakeA, snakeB }, HitSize);
        var reversed = _rules.FindCollided(new[] { snakeB, snakeA }, HitSize);

        Assert.Equivalent(forward, reversed);
    }

    [Fact]
    public void FindCollided_NonPositiveHitSize_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => _rules.FindCollided(Array.Empty<SnakeBody>(), 0));

    [Fact]
    public void FindCollisions_NamesTheSnakeEachOneRanInto()
    {
        var snakeA = Snake(A, (100, 100), (105, 100), (110, 100));
        var snakeB = Snake(B, (200, 52), (200, 100), (200, 148));
        var intruder = Snake(C, (400, 400), (205, 100));

        var collisions = _rules.FindCollisions(new[] { snakeA, snakeB, intruder }, HitSize);

        Assert.Equal(new Dictionary<Guid, Guid> { [C] = B }, collisions);
    }

    [Fact]
    public void FindCollisions_AHeadOnHitNamesEachOther()
    {
        var snakeA = Snake(A, (100, 100), (200, 100));
        var snakeB = Snake(B, (300, 100), (205, 100));

        var collisions = _rules.FindCollisions(new[] { snakeA, snakeB }, HitSize);

        Assert.Equal(B, collisions[A]);
        Assert.Equal(A, collisions[B]);
    }
}
