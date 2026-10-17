using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class DeathDropRulesTests
{
    private readonly DeathDropRules _rules = new(new SnakeRulesOptions { MaxDeathPellets = 40 });

    private static IReadOnlyList<Point> Body(int length)
        => Enumerable.Range(0, length).Select(index => new Point(index * 10, 0)).ToArray();

    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(40)]
    [InlineData(41)]
    [InlineData(1000)]
    [InlineData(12345)]
    public void Plan_AnyMass_PreservesTotalValue(long mass)
        => Assert.Equal(mass, _rules.Plan(mass, Body(20)).Sum(pellet => pellet.Value));

    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 7)]
    [InlineData(40, 40)]
    [InlineData(400, 40)]
    public void Plan_PelletCount_IsMassCappedAtMaximum(long mass, int expectedCount)
        => Assert.Equal(expectedCount, _rules.Plan(mass, Body(20)).Count);

    [Fact]
    public void Plan_MassAboveCap_SpreadsRemainderOverFirstPellets()
    {
        var values = _rules.Plan(43, Body(20)).Select(pellet => pellet.Value).ToArray();

        Assert.Equal(3, values.Count(value => value == 2));
        Assert.Equal(37, values.Count(value => value == 1));
        Assert.Equal(new long[] { 2, 2, 2 }, values.Take(3));
    }

    [Fact]
    public void Plan_EveryPelletHasAtLeastOneValue()
        => Assert.All(_rules.Plan(97, Body(20)), pellet => Assert.True(pellet.Value >= 1));

    [Fact]
    public void Plan_Positions_AreSpreadAlongTheWholeBody()
    {
        var body = Body(10);

        var positions = _rules.Plan(5, body).Select(pellet => pellet.Position).ToArray();

        Assert.Equal(new[] { body[0], body[2], body[4], body[6], body[8] }, positions);
    }

    [Fact]
    public void Plan_PositionsAlwaysComeFromTheBody()
    {
        var body = Body(3);

        Assert.All(_rules.Plan(25, body), pellet => Assert.Contains(pellet.Position, body));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Plan_NoMass_DropsNothing(long mass)
        => Assert.Empty(_rules.Plan(mass, Body(5)));

    [Fact]
    public void Plan_EmptyBody_DropsNothing()
        => Assert.Empty(_rules.Plan(10, Array.Empty<Point>()));

    [Fact]
    public void Constructor_InvalidMaxPellets_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new DeathDropRules(new SnakeRulesOptions { MaxDeathPellets = 0 }));
}
