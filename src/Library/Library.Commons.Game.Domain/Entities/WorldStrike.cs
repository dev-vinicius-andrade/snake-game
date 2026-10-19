namespace Library.Commons.Game.Domain.Entities;

/// <summary>What is about to hit a spot of the map.</summary>
public enum WorldStrikeKind
{
    Lightning = 0,
    Meteor = 1
}

/// <summary>A spot of the map that is about to be struck, shown to the players until it is.</summary>
/// <param name="Radius">World units around the spot that the strike reaches.</param>
/// <param name="StrikesAt">The moment it strikes.</param>
/// <param name="Kind">What strikes: the players see lightning and meteors differently.</param>
public readonly record struct WorldStrike(double X, double Y, double Radius, DateTimeOffset StrikesAt, WorldStrikeKind Kind = WorldStrikeKind.Lightning);
