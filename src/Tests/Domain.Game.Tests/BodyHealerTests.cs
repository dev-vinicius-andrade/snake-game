using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class BodyHealerTests
{
    private readonly BodyHealer _healer = new();

    private static SegmentHealth Seg(double hp) => SegmentHealth.Of(hp, 100);

    [Fact]
    public void Heal_MostDamagedSegment_ReceivesHealFirst()
    {
        var body = new[] { Seg(100), Seg(80), Seg(20), Seg(60) };

        var healed = _healer.Heal(body, 30);

        Assert.Equal(new double[] { 100, 80, 50, 60 }, healed.Select(s => s.Hp));
    }

    [Fact]
    public void Heal_Overflow_FlowsToNextMostDamaged()
    {
        var body = new[] { Seg(100), Seg(80), Seg(20), Seg(60) };

        var healed = _healer.Heal(body, 100);

        Assert.Equal(new double[] { 100, 80, 100, 80 }, healed.Select(s => s.Hp));
    }

    [Fact]
    public void Heal_TieOnRatio_ClosestToHeadWins()
    {
        var body = new[] { Seg(100), Seg(50), Seg(50) };

        var healed = _healer.Heal(body, 10);

        Assert.Equal(new double[] { 100, 60, 50 }, healed.Select(s => s.Hp));
    }

    [Fact]
    public void Heal_PoolBiggerThanDamage_HealsEverythingAndStops()
    {
        var body = new[] { Seg(100), Seg(10), Seg(90) };

        var healed = _healer.Heal(body, 10_000);

        Assert.All(healed, segment => Assert.False(segment.IsDamaged));
    }

    [Fact]
    public void Heal_NothingDamaged_ReturnsEqualBody()
    {
        var body = new[] { Seg(100), Seg(100) };

        Assert.Equal(body, _healer.Heal(body, 50));
    }

    [Fact]
    public void Heal_DoesNotMutateInput()
    {
        var body = new[] { Seg(100), Seg(10) };

        _healer.Heal(body, 50);

        Assert.Equal(10, body[1].Hp);
    }

    [Fact]
    public void Heal_DifferentMaxHp_ComparesRatioNotAbsoluteHp()
    {
        var body = new[] { SegmentHealth.Of(150, 300), SegmentHealth.Of(40, 100) };

        var healed = _healer.Heal(body, 10);

        Assert.Equal(new double[] { 150, 50 }, healed.Select(s => s.Hp));
    }

    [Fact]
    public void Heal_NegativeAmount_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => _healer.Heal(new[] { Seg(50) }, -1));

    [Fact]
    public void Heal_NullBody_Throws()
        => Assert.Throws<ArgumentNullException>(() => _healer.Heal(null!, 1));
}
