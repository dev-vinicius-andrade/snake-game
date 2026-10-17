using Domain.Game.Simulation;

namespace Domain.Game.Tests;

public sealed class FixedTimestepClockTests
{
    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(10);

    [Fact]
    public void Advance_LessThanOneStep_ReturnsNoTicks()
        => Assert.Empty(new FixedTimestepClock(Step).Advance(TimeSpan.FromMilliseconds(9)));

    [Fact]
    public void Advance_PartialTimeAccumulates_AcrossCalls()
    {
        var clock = new FixedTimestepClock(Step);

        clock.Advance(TimeSpan.FromMilliseconds(6));
        var ticks = clock.Advance(TimeSpan.FromMilliseconds(6));

        Assert.Single(ticks);
    }

    [Fact]
    public void Advance_RemainderIsKept_NotDiscarded()
    {
        var clock = new FixedTimestepClock(Step);

        var first = clock.Advance(TimeSpan.FromMilliseconds(25));
        var second = clock.Advance(TimeSpan.FromMilliseconds(5));

        Assert.Equal(2, first.Count);
        Assert.Single(second);
    }

    [Fact]
    public void Advance_ManyStepsDue_CapsAndDropsExcess()
    {
        var clock = new FixedTimestepClock(Step, maxStepsPerAdvance: 3);

        var stalled = clock.Advance(TimeSpan.FromSeconds(10));
        var next = clock.Advance(TimeSpan.FromMilliseconds(5));

        Assert.Equal(3, stalled.Count);
        Assert.Empty(next);
    }

    [Fact]
    public void Advance_TickNumbers_IncreaseAcrossCalls()
    {
        var clock = new FixedTimestepClock(Step);

        var numbers = clock.Advance(TimeSpan.FromMilliseconds(20))
            .Concat(clock.Advance(TimeSpan.FromMilliseconds(10)))
            .Select(tick => tick.Number);

        Assert.Equal(new long[] { 1, 2, 3 }, numbers);
    }

    [Fact]
    public void Advance_SnapshotEveryTwo_FlagsEverySecondTick()
    {
        var clock = new FixedTimestepClock(Step, snapshotEveryTicks: 2);

        var flags = clock.Advance(TimeSpan.FromMilliseconds(40)).Select(tick => tick.BroadcastSnapshot);

        Assert.Equal(new[] { false, true, false, true }, flags);
    }

    [Fact]
    public void Advance_NegativeElapsed_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new FixedTimestepClock(Step).Advance(TimeSpan.FromMilliseconds(-1)));

    [Theory]
    [InlineData(0, 5, 2)]
    [InlineData(10, 0, 2)]
    [InlineData(10, 5, 0)]
    public void Constructor_InvalidArguments_Throws(int stepMilliseconds, int maxSteps, int snapshotEvery)
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => new FixedTimestepClock(TimeSpan.FromMilliseconds(stepMilliseconds), maxSteps, snapshotEvery));
}
