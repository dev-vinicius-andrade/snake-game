namespace Library.Commons.Game.Domain.Entities;

/// <summary>A spot of the map that lightning is about to strike, shown to the players until it does.</summary>
/// <param name="Radius">World units around the spot that the strike reaches.</param>
/// <param name="StrikesAt">The moment it strikes.</param>
public readonly record struct WorldStrike(double X, double Y, double Radius, DateTimeOffset StrikesAt);
