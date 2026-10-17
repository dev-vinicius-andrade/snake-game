namespace Domain.Game.Rules;

public readonly record struct WorldBounds(double Width, double Height);

/// <summary>
/// Body of one snake. Both lists are ordered tail to head (the head is the last element)
/// and aligned: <c>Health[i]</c> is the HP ratio (0..1) of <c>Segments[i]</c>.
/// </summary>
public sealed record BodyState(IReadOnlyList<Point> Segments, IReadOnlyList<double> Health);
