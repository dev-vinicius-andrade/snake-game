using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class BodyHealthRulesTests
{
    private readonly BodyHealthRules _rules = new(new BodyHealer());

    [Fact]
    public void Heal_MostDamagedSegment_GetsTheHp()
    {
        var healed = _rules.Heal(new[] { 1.0, 0.2, 0.6 }, maxHp: 100, amount: 30);

        Assert.Equal(new[] { 1.0, 0.5, 0.6 }, healed.Select(ratio => Math.Round(ratio, 6)));
    }

    [Fact]
    public void Heal_Overflow_ContinuesOnNextMostDamaged()
    {
        var healed = _rules.Heal(new[] { 1.0, 0.9, 0.6 }, maxHp: 100, amount: 45);

        Assert.Equal(new[] { 1.0, 0.95, 1.0 }, healed.Select(ratio => Math.Round(ratio, 6)));
    }

    [Fact]
    public void Heal_ScalesWithMaxHp()
    {
        var healed = _rules.Heal(new[] { 0.5 }, maxHp: 200, amount: 50);

        Assert.Equal(0.75, healed[0], 6);
    }

    [Fact]
    public void Heal_NothingDamaged_ReturnsSameRatios()
        => Assert.Equal(new[] { 1.0, 1.0 }, _rules.Heal(new[] { 1.0, 1.0 }, 100, 40));

    [Fact]
    public void Heal_Always_DoesNotMutateInput()
    {
        var ratios = new[] { 0.5, 1.0 };

        _rules.Heal(ratios, 100, 20);

        Assert.Equal(0.5, ratios[0]);
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(100, -1)]
    public void Heal_InvalidArguments_Throw(double maxHp, double amount)
        => Assert.Throws<ArgumentOutOfRangeException>(() => _rules.Heal(new[] { 0.5 }, maxHp, amount));
}
