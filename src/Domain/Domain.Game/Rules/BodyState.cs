namespace Domain.Game.Rules;

public readonly record struct WorldBounds(double Width, double Height);

/// <summary>
/// Body of one snake. <c>Segments</c> and <c>Health</c> are ordered tail to head (the head is the last element)
/// and aligned: <c>Health[i]</c> is the HP ratio (0..1) of <c>Segments[i]</c>.
/// <c>Trail</c> is where the head has been, oldest first and the head last: the segments are placed along it.
/// It is empty for a snake that has not moved yet, and the segments then stand in for it.
/// </summary>
public sealed record BodyState(IReadOnlyList<Point> Segments, IReadOnlyList<double> Health, IReadOnlyList<Point>? Trail = null)
{
    public IReadOnlyList<Point> Trail { get; init; } = Trail ?? Array.Empty<Point>();
}
