namespace Domain.Game.Rules;

/// <summary>
/// Heading maths. Angles are radians in screen coordinates: 0 points right, positive turns clockwise
/// (y grows downwards), so up is -PI/2.
/// </summary>
public sealed class HeadingRules
{
    private const double Epsilon = 1e-12;

    private readonly SnakeRulesOptions _options;

    public HeadingRules(SnakeRulesOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.EnsureValid();
        _options = options;
    }

    /// <summary>Wraps an angle into (-PI, PI].</summary>
    public double Normalize(double angle)
    {
        EnsureFinite(angle, nameof(angle));
        var wrapped = angle % (2 * Math.PI);
        if (wrapped <= -Math.PI + Epsilon) return wrapped + 2 * Math.PI;
        return wrapped > Math.PI + Epsilon ? wrapped - 2 * Math.PI : wrapped;
    }

    /// <summary>
    /// Turns from <paramref name="current"/> toward <paramref name="target"/> by at most <paramref name="maxStep"/>
    /// radians, the short way round. Exactly opposite headings turn clockwise so the result is deterministic.
    /// </summary>
    public double Turn(double current, double target, double maxStep)
    {
        EnsureFinite(maxStep, nameof(maxStep));
        if (maxStep < 0) throw new ArgumentOutOfRangeException(nameof(maxStep), maxStep, "Must not be negative.");

        var delta = Normalize(target - current);
        return Math.Abs(delta) <= maxStep
            ? Normalize(target)
            : Normalize(current + Math.Sign(delta) * maxStep);
    }

    /// <summary>Largest turn allowed in one tick at the configured turn rate.</summary>
    public double MaxTurn(TimeSpan tickRate)
    {
        if (tickRate <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(tickRate), tickRate, "Must be greater than zero.");
        return _options.TurnRateDegreesPerSecond * Math.PI / 180 * tickRate.TotalSeconds;
    }

    /// <summary>Unit vector for a heading.</summary>
    public Point AxisOf(double angle) => new(Math.Cos(angle), Math.Sin(angle));

    public double AngleOf(double x, double y) => Math.Atan2(y, x);

    private static void EnsureFinite(double value, string name)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(name, value, "Must be a finite number.");
    }
}
