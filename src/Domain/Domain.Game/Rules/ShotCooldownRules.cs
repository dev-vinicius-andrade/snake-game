namespace Domain.Game.Rules;

/// <summary>Shot cooldown counted in simulation ticks, so it follows the fixed timestep and not the wall clock.</summary>
public sealed class ShotCooldownRules
{
    private const double QuantizationSlack = 0.01;

    private readonly SnakeRulesOptions _options;

    public ShotCooldownRules(SnakeRulesOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.EnsureValid();
        _options = options;
    }

    /// <summary>
    /// Ticks to wait after a shot: the cooldown divided by the tick length, rounded up, at least 1.
    /// A tick length is a <see cref="TimeSpan"/>, so 1000/60 ms is stored slightly short; a hundredth of a tick of
    /// slack keeps 250 ms at 60 Hz at 15 ticks instead of 16.
    /// </summary>
    public int CooldownTicks(TimeSpan tickRate)
    {
        if (tickRate <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(tickRate), tickRate, "Must be greater than zero.");
        var ticks = (int)Math.Ceiling(_options.ShotCooldownMilliseconds / tickRate.TotalMilliseconds - QuantizationSlack);
        return Math.Max(1, ticks);
    }

    public int Tick(int remainingTicks) => Math.Max(0, remainingTicks - 1);

    public bool CanShoot(int remainingTicks) => remainingTicks <= 0;
}
