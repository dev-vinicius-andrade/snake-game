namespace Library.Commons.Game.Domain.Entities;

/// <summary>A mine lying on the map of a room until a snake touches it. It is shown from the start and can only be set off once armed.</summary>
/// <param name="Radius">World units around the mine that its blast reaches.</param>
/// <param name="TriggerRadius">How close a head must come to set it off.</param>
/// <param name="ArmedAt">The moment it can be set off.</param>
public sealed record WorldMine(Guid Id, double X, double Y, double Radius, double TriggerRadius, DateTimeOffset ArmedAt);

/// <summary>A snake that is wanted: whoever kills it is paid. It ends when the snake dies or the time is up.</summary>
/// <param name="Target">The guid of the wanted snake.</param>
/// <param name="EndsAt">When the mark is taken off if nobody killed it.</param>
/// <param name="RewardMass">Mass the killer gets.</param>
/// <param name="FoodsCredit">Foods the kill counts for toward the perks of the killer.</param>
/// <param name="Reward">What else the killer gets: a random perk, lives, a tier surge, mass.</param>
public sealed record WorldBounty(Guid Target, DateTimeOffset EndsAt, long RewardMass, int FoodsCredit, PerkReward? Reward = null);

/// <summary>The walls of a maze as a grid of square blocks, each a wall or open floor. Everything outside the grid is open.</summary>
public interface IMazeWalls
{
    int BlockColumns { get; }
    int BlockRows { get; }
    bool IsWall(int column, int row);
}

/// <summary>
/// A maze standing in a room for a while. The walls are a grid of blocks (<see cref="Unit"/> world units a side, one head size) from the
/// top left corner at <see cref="X"/>, <see cref="Y"/>; the server and the client build the same walls from the seed. Walls stop heads and
/// shots and cannot be broken.
/// </summary>
/// <param name="Columns">Cells across.</param>
/// <param name="Rows">Cells down.</param>
/// <param name="Corridor">Blocks across a corridor.</param>
/// <param name="Twistiness">0 to 100, how twisty the corridors are (it shaped the walls).</param>
/// <param name="PrizeId">The food that is the prize at the end; the maze is over when it is gone.</param>
/// <param name="WallMassLoss">Mass a snake loses when its head runs into a wall.</param>
public sealed record WorldMaze(
    double X, double Y, int Columns, int Rows, int Corridor, int Twistiness, uint Seed, double Unit,
    DateTimeOffset EndsAt, IMazeWalls Walls, Guid PrizeId, long WallMassLoss)
{
    /// <summary>When each snake last paid for touching a wall, so scraping along one is not charged every frame.</summary>
    public System.Collections.Concurrent.ConcurrentDictionary<Guid, DateTimeOffset> WallHits { get; } = new();

    /// <summary>Whether the point is inside a wall. One lookup.</summary>
    public bool BlocksPoint(double x, double y)
        => Walls.IsWall((int)Math.Floor((x - X) / Unit), (int)Math.Floor((y - Y) / Unit));

    /// <summary>Whether the square of this size with its top left corner here overlaps a wall. At most four lookups for a square of one block.</summary>
    public bool BlocksSquare(double left, double top, double size)
    {
        var firstColumn = (int)Math.Floor((left - X) / Unit);
        var lastColumn = (int)Math.Ceiling((left + size - X) / Unit) - 1;
        var firstRow = (int)Math.Floor((top - Y) / Unit);
        var lastRow = (int)Math.Ceiling((top + size - Y) / Unit) - 1;
        for (var row = firstRow; row <= lastRow; row++)
            for (var column = firstColumn; column <= lastColumn; column++)
                if (Walls.IsWall(column, row)) return true;
        return false;
    }
}
