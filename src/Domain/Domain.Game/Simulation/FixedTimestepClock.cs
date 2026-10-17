namespace Domain.Game.Simulation;

/// <summary>One fixed simulation step. <see cref="BroadcastSnapshot"/> marks the steps after which clients get a snapshot.</summary>
public readonly record struct SimulationTick(long Number, bool BroadcastSnapshot);

/// <summary>
/// Turns variable wall-clock time into a whole number of fixed simulation steps.
/// Not thread safe: use one instance per room, driven by one loop.
/// </summary>
public sealed class FixedTimestepClock
{
    private readonly TimeSpan _stepDuration;
    private readonly int _maxStepsPerAdvance;
    private readonly int _snapshotEveryTicks;
    private TimeSpan _accumulated = TimeSpan.Zero;
    private long _tickNumber;

    public FixedTimestepClock(TimeSpan stepDuration, int maxStepsPerAdvance = 5, int snapshotEveryTicks = 2)
    {
        if (stepDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(stepDuration), stepDuration, "Must be greater than zero.");
        if (maxStepsPerAdvance < 1)
            throw new ArgumentOutOfRangeException(nameof(maxStepsPerAdvance), maxStepsPerAdvance, "Must be at least 1.");
        if (snapshotEveryTicks < 1)
            throw new ArgumentOutOfRangeException(nameof(snapshotEveryTicks), snapshotEveryTicks, "Must be at least 1.");

        _stepDuration = stepDuration;
        _maxStepsPerAdvance = maxStepsPerAdvance;
        _snapshotEveryTicks = snapshotEveryTicks;
    }

    /// <summary>
    /// Adds elapsed time and returns the steps that are due. When more than the cap are due the
    /// excess time is dropped, so a stall never causes a catch-up spiral.
    /// </summary>
    public IReadOnlyList<SimulationTick> Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(elapsed), elapsed, "Must not be negative.");

        _accumulated += elapsed;
        var due = _accumulated.Ticks / _stepDuration.Ticks;
        var steps = (int)Math.Min(due, _maxStepsPerAdvance);
        _accumulated = due > _maxStepsPerAdvance ? TimeSpan.Zero : _accumulated - _stepDuration * steps;
        return Enumerable.Range(0, steps).Select(_ => NextTick()).ToArray();
    }

    private SimulationTick NextTick()
    {
        _tickNumber++;
        return new SimulationTick(_tickNumber, _tickNumber % _snapshotEveryTicks == 0);
    }
}
