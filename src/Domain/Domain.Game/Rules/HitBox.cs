namespace Domain.Game.Rules;

internal static class HitBox
{
    /// <summary>Axis aligned squares of the given size anchored at the points; touching counts as overlap.</summary>
    internal static bool Overlaps(Point first, Point second, double size)
        => first.X <= second.X + size && first.X + size >= second.X
        && first.Y <= second.Y + size && first.Y + size >= second.Y;
}
