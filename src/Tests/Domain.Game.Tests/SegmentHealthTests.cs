using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class SegmentHealthTests
{
    [Fact]
    public void Full_MaxHpGiven_StartsUndamaged()
    {
        var segment = SegmentHealth.Full(100);

        Assert.Equal(1, segment.Ratio);
        Assert.False(segment.IsDamaged);
        Assert.False(segment.IsBroken);
    }

    [Fact]
    public void TakeDamage_PartialDamage_ReturnsNewInstanceAndKeepsOriginal()
    {
        var original = SegmentHealth.Full(100);

        var damaged = original.TakeDamage(30);

        Assert.Equal(70, damaged.Hp);
        Assert.Equal(100, original.Hp);
    }

    [Fact]
    public void TakeDamage_MoreThanHp_BreaksAndFloorsAtZero()
    {
        var segment = SegmentHealth.Full(100).TakeDamage(250);

        Assert.Equal(0, segment.Hp);
        Assert.True(segment.IsBroken);
    }

    [Fact]
    public void Heal_MoreThanMissing_CapsAtMaxHp()
        => Assert.Equal(100, SegmentHealth.Of(90, 100).Heal(50).Hp);

    [Fact]
    public void WithMaxHp_Grown_KeepsRatio()
    {
        var segment = SegmentHealth.Of(50, 100).WithMaxHp(200);

        Assert.Equal(100, segment.Hp);
        Assert.Equal(0.5, segment.Ratio);
    }

    [Fact]
    public void Of_HpAboveMax_Clamps()
        => Assert.Equal(100, SegmentHealth.Of(500, 100).Hp);

    [Fact]
    public void TakeDamage_Negative_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => SegmentHealth.Full(100).TakeDamage(-1));

    [Fact]
    public void Full_NonPositiveMax_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => SegmentHealth.Full(0));
}
