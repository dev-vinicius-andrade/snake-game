using Library.Commons.Game.Domain.Entities;

namespace Domain.Game.Rules;

/// <summary>Which side of the maze its entrance is on.</summary>
public enum MazeSide
{
    North,
    East,
    South,
    West
}

/// <summary>
/// A generated maze: which cells connect, where the entrance is and which cell holds the prize. As a grid of square blocks it is
/// a <see cref="IMazeWalls"/>: a wall is one block thick, a corridor is <see cref="Corridor"/> blocks wide, and a cell and the wall after
/// it span <c>Corridor + 1</c> blocks. The corner blocks of every wall line are always wall.
/// </summary>
public sealed class MazeLayout : IMazeWalls
{
    private readonly bool[] _openEast;
    private readonly bool[] _openSouth;

    internal MazeLayout(
        int columns, int rows, int corridor, bool[] openEast, bool[] openSouth, MazeSide entranceSide, int entranceCell, int prizeCell)
    {
        Columns = columns;
        Rows = rows;
        Corridor = corridor;
        _openEast = openEast;
        _openSouth = openSouth;
        EntranceSide = entranceSide;
        EntranceCell = entranceCell;
        PrizeCell = prizeCell;
    }

    public int Columns { get; }
    public int Rows { get; }

    /// <summary>Blocks across a corridor.</summary>
    public int Corridor { get; }

    public MazeSide EntranceSide { get; }

    /// <summary>The cell, counted row by row, whose side on the outer wall is open.</summary>
    public int EntranceCell { get; }

    /// <summary>The cell farthest from the entrance by the way through the maze.</summary>
    public int PrizeCell { get; }

    private int Pitch => Corridor + 1;

    public int BlockColumns => Columns * Pitch + 1;
    public int BlockRows => Rows * Pitch + 1;

    /// <summary>Whether the cell connects to the one on its east side.</summary>
    public bool IsOpenEast(int cell) => _openEast[cell];

    /// <summary>Whether the cell connects to the one on its south side.</summary>
    public bool IsOpenSouth(int cell) => _openSouth[cell];

    /// <summary>Passages that are open, which is a tree's worth plus the loops.</summary>
    public int OpenPassages => _openEast.Count(open => open) + _openSouth.Count(open => open);

    public bool IsWall(int column, int row)
    {
        if (column < 0 || row < 0 || column >= BlockColumns || row >= BlockRows) return false;
        var inColumn = column % Pitch;
        var inRow = row % Pitch;
        if (inColumn != 0 && inRow != 0) return false;
        if (inColumn == 0 && inRow == 0) return true;
        if (inColumn == 0)
        {
            var line = column / Pitch;
            var cellRow = row / Pitch;
            if (line == 0) return !(EntranceSide == MazeSide.West && EntranceCell == cellRow * Columns);
            if (line == Columns) return !(EntranceSide == MazeSide.East && EntranceCell == cellRow * Columns + Columns - 1);
            return !_openEast[cellRow * Columns + line - 1];
        }
        else
        {
            var line = row / Pitch;
            var cellColumn = column / Pitch;
            if (line == 0) return !(EntranceSide == MazeSide.North && EntranceCell == cellColumn);
            if (line == Rows) return !(EntranceSide == MazeSide.South && EntranceCell == (Rows - 1) * Columns + cellColumn);
            return !_openSouth[(line - 1) * Columns + cellColumn];
        }
    }

    /// <summary>The block that is the middle of a cell (its first block plus half the corridor), as a column and a row of blocks.</summary>
    public (double Column, double Row) CellCenter(int cell)
        => ((cell % Columns) * Pitch + 1 + Corridor / 2.0, (cell / Columns) * Pitch + 1 + Corridor / 2.0);

    /// <summary>The walls drawn in text, one line per row of blocks, <c>#</c> for a wall and <c>.</c> for floor. The shared test vector.</summary>
    public string ToAscii()
    {
        var lines = new string[BlockRows];
        for (var row = 0; row < BlockRows; row++)
            lines[row] = string.Create(BlockColumns, row, (span, y) =>
            {
                for (var column = 0; column < span.Length; column++) span[column] = IsWall(column, y) ? '#' : '.';
            });
        return string.Join('\n', lines);
    }
}

/// <summary>
/// Builds mazes from a seed. Pure and deterministic: the same seed and parameters give the same maze, on the server and in the client
/// (the TypeScript port in <c>Application.Ui/src/game/maze.ts</c> must match; both are checked against the vector in
/// <c>src/Tests/Shared/maze-vector.json</c>). Only integer arithmetic is used, so no rounding can tell the two apart.
///
/// The cells are carved by a depth first search (the "recursive backtracker"): a long winding corridor with many dead ends. At every step
/// the walk keeps going straight, if it can, unless a draw (<c>twistiness</c> out of 100) says to turn; at 100 it always picks a
/// direction at random, at 0 it goes straight as far as it can. Afterwards <see cref="LoopsPerHundredCells"/> walls per hundred cells
/// are opened at random, which makes a few loops.
/// </summary>
public static class MazeGenerator
{
    public const int MinCells = 3;
    public const int MaxCells = 30;
    public const int MinCorridor = 3;
    public const int MaxCorridor = 12;

    /// <summary>Walls opened after the carving, per hundred cells (at most; one that was open already is not counted).</summary>
    public const int LoopsPerHundredCells = 6;

    public static MazeLayout Generate(uint seed, int columns, int rows, int twistiness, int corridor)
    {
        if (columns is < MinCells or > MaxCells) throw new ArgumentOutOfRangeException(nameof(columns), columns, $"Must be {MinCells} to {MaxCells}.");
        if (rows is < MinCells or > MaxCells) throw new ArgumentOutOfRangeException(nameof(rows), rows, $"Must be {MinCells} to {MaxCells}.");
        if (twistiness is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(twistiness), twistiness, "Must be 0 to 100.");
        if (corridor is < MinCorridor or > MaxCorridor)
            throw new ArgumentOutOfRangeException(nameof(corridor), corridor, $"Must be {MinCorridor} to {MaxCorridor}.");

        var random = new MazeRandom(seed);
        var cells = columns * rows;
        var openEast = new bool[cells];
        var openSouth = new bool[cells];
        Carve(random, columns, rows, twistiness, openEast, openSouth);
        OpenLoops(random, columns, rows, openEast, openSouth);
        var (side, entrance) = PickEntrance(random, columns, rows);
        var prize = Farthest(columns, rows, entrance, openEast, openSouth);
        return new MazeLayout(columns, rows, corridor, openEast, openSouth, side, entrance, prize);
    }

    // North, east, south, west: the order matters, it is part of the vector.
    private static readonly int[] StepColumn = [0, 1, 0, -1];
    private static readonly int[] StepRow = [-1, 0, 1, 0];

    private static void Carve(MazeRandom random, int columns, int rows, int twistiness, bool[] openEast, bool[] openSouth)
    {
        var cells = columns * rows;
        var visited = new bool[cells];
        var stack = new int[cells];
        var arrivedBy = new int[cells];
        var depth = 0;
        var start = (int)random.Below((uint)cells);
        visited[start] = true;
        stack[0] = start;
        arrivedBy[0] = -1;
        depth = 1;
        var options = new int[4];
        while (depth > 0)
        {
            var cell = stack[depth - 1];
            var count = 0;
            for (var direction = 0; direction < 4; direction++)
                if (CanVisit(cell, direction, columns, rows, visited)) options[count++] = direction;
            if (count == 0)
            {
                depth--;
                continue;
            }

            var came = arrivedBy[depth - 1];
            var chosen = -1;
            if (came >= 0 && Array.IndexOf(options, came, 0, count) >= 0 && random.Below(100) >= (uint)twistiness) chosen = came;
            if (chosen < 0) chosen = options[random.Below((uint)count)];

            var next = (cell / columns + StepRow[chosen]) * columns + cell % columns + StepColumn[chosen];
            Open(cell, chosen, columns, openEast, openSouth);
            visited[next] = true;
            stack[depth] = next;
            arrivedBy[depth] = chosen;
            depth++;
        }
    }

    private static bool CanVisit(int cell, int direction, int columns, int rows, bool[] visited)
    {
        var column = cell % columns + StepColumn[direction];
        var row = cell / columns + StepRow[direction];
        return column >= 0 && row >= 0 && column < columns && row < rows && !visited[row * columns + column];
    }

    /// <summary>Opens the passage from a cell in a direction (the passage is stored on the north or west cell of the two).</summary>
    private static void Open(int cell, int direction, int columns, bool[] openEast, bool[] openSouth)
    {
        switch (direction)
        {
            case 0: openSouth[cell - columns] = true; break;
            case 1: openEast[cell] = true; break;
            case 2: openSouth[cell] = true; break;
            default: openEast[cell - 1] = true; break;
        }
    }

    private static void OpenLoops(MazeRandom random, int columns, int rows, bool[] openEast, bool[] openSouth)
    {
        var cells = columns * rows;
        var attempts = Math.Max(1, cells * LoopsPerHundredCells / 100);
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var cell = (int)random.Below((uint)cells);
            var south = random.Below(2) == 1;
            if (south && cell / columns < rows - 1) openSouth[cell] = true;
            else if (!south && cell % columns < columns - 1) openEast[cell] = true;
        }
    }

    private static (MazeSide Side, int Cell) PickEntrance(MazeRandom random, int columns, int rows)
    {
        var side = (MazeSide)random.Below(4);
        return side switch
        {
            MazeSide.North => (side, (int)random.Below((uint)columns)),
            MazeSide.South => (side, (rows - 1) * columns + (int)random.Below((uint)columns)),
            MazeSide.West => (side, (int)random.Below((uint)rows) * columns),
            _ => (side, (int)random.Below((uint)rows) * columns + columns - 1)
        };
    }

    /// <summary>The cell farthest from the start by the way through the maze; the lowest cell wins a tie.</summary>
    private static int Farthest(int columns, int rows, int start, bool[] openEast, bool[] openSouth)
    {
        var cells = columns * rows;
        var distance = new int[cells];
        Array.Fill(distance, -1);
        var queue = new int[cells];
        var head = 0;
        var tail = 0;
        distance[start] = 0;
        queue[tail++] = start;
        while (head < tail)
        {
            var cell = queue[head++];
            var column = cell % columns;
            var row = cell / columns;
            if (row > 0 && openSouth[cell - columns]) Visit(cell - columns);
            if (column < columns - 1 && openEast[cell]) Visit(cell + 1);
            if (row < rows - 1 && openSouth[cell]) Visit(cell + columns);
            if (column > 0 && openEast[cell - 1]) Visit(cell - 1);

            void Visit(int neighbor)
            {
                if (distance[neighbor] >= 0) return;
                distance[neighbor] = distance[cell] + 1;
                queue[tail++] = neighbor;
            }
        }

        var farthest = start;
        for (var cell = 0; cell < cells; cell++)
            if (distance[cell] > distance[farthest]) farthest = cell;
        return farthest;
    }
}

/// <summary>
/// A small generator that gives the same numbers from the same seed anywhere (mulberry32). <c>Below(n)</c> is the next number modulo n.
/// The modulo bias is of no matter here, and it is the same in the TypeScript port.
/// </summary>
internal sealed class MazeRandom(uint seed)
{
    private uint _state = seed;

    public uint Next()
    {
        unchecked
        {
            _state += 0x6D2B79F5u;
            var t = _state;
            t = (t ^ (t >> 15)) * (t | 1u);
            t ^= t + (t ^ (t >> 7)) * (t | 61u);
            return t ^ (t >> 14);
        }
    }

    public uint Below(uint limit) => Next() % limit;
}
