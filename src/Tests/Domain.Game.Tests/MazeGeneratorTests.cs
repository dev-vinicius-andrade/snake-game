using System.Text.Json;
using Domain.Game.Rules;

namespace Domain.Game.Tests;

public sealed class MazeGeneratorTests
{
    private static MazeLayout Make(uint seed = 7, int columns = 8, int rows = 6, int twistiness = 50, int corridor = 3)
        => MazeGenerator.Generate(seed, columns, rows, twistiness, corridor);

    [Fact]
    public void Generate_SameSeedAndParameters_GiveTheSameMaze()
        => Assert.Equal(Make(42).ToAscii(), Make(42).ToAscii());

    [Fact]
    public void Generate_OtherSeeds_GiveOtherMazes()
        => Assert.NotEqual(Make(1).ToAscii(), Make(2).ToAscii());

    [Fact]
    public void Generate_AMazeIsBlocksOfAWallAroundCellsOfCorridorWidth()
    {
        var maze = Make(columns: 5, rows: 4, corridor: 4);

        Assert.Equal(5 * 5 + 1, maze.BlockColumns);
        Assert.Equal(4 * 5 + 1, maze.BlockRows);
    }

    [Fact]
    public void IsWall_OutsideTheGrid_IsOpen()
    {
        var maze = Make();

        Assert.False(maze.IsWall(-1, 0));
        Assert.False(maze.IsWall(0, -1));
        Assert.False(maze.IsWall(maze.BlockColumns, 0));
        Assert.False(maze.IsWall(0, maze.BlockRows));
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(99u)]
    [InlineData(123456u)]
    [InlineData(4000000000u)]
    public void Generate_EveryCellCanBeReachedFromTheEntrance(uint seed)
    {
        var maze = Make(seed, columns: 9, rows: 7);

        Assert.DoesNotContain(-1, Distances(maze));
    }

    [Theory]
    [InlineData(1u, 0)]
    [InlineData(2u, 50)]
    [InlineData(3u, 100)]
    public void Generate_ThereAreLoopsAndDeadEnds_NotJustOnePath(uint seed, int twistiness)
    {
        var maze = Make(seed, columns: 12, rows: 10, twistiness: twistiness);
        var cells = maze.Columns * maze.Rows;

        // A spanning tree has cells - 1 passages; more means loops.
        Assert.True(maze.OpenPassages > cells - 1);
        Assert.True(DeadEnds(maze) > 0);
    }

    [Fact]
    public void Generate_TheOuterWallIsClosedExceptForTheEntrance()
    {
        var maze = Make(11);
        var openings = 0;
        for (var column = 0; column < maze.BlockColumns; column++)
        {
            if (!maze.IsWall(column, 0)) openings++;
            if (!maze.IsWall(column, maze.BlockRows - 1)) openings++;
        }
        for (var row = 0; row < maze.BlockRows; row++)
        {
            if (!maze.IsWall(0, row)) openings++;
            if (!maze.IsWall(maze.BlockColumns - 1, row)) openings++;
        }

        Assert.Equal(maze.Corridor, openings);
    }

    [Fact]
    public void Generate_ThePrizeIsFarFromTheEntrance_NotNextToIt()
    {
        foreach (var seed in new uint[] { 1, 2, 3, 4, 5, 6 })
        {
            var maze = Make(seed, columns: 10, rows: 10);

            Assert.NotEqual(maze.EntranceCell, maze.PrizeCell);
            Assert.True(Distances(maze)[maze.PrizeCell] >= (maze.Columns + maze.Rows) / 2, $"seed {seed}");
        }
    }

    [Fact]
    public void Generate_ThePrizeCellIsTheFarthestOne()
    {
        var maze = Make(5, columns: 10, rows: 10);
        var distance = Distances(maze);

        Assert.Equal(distance.Max(), distance[maze.PrizeCell]);
    }

    [Fact]
    public void Generate_NoTwistiness_BendsLessThanFullTwistiness()
    {
        var straight = Enumerable.Range(1, 12).Sum(seed => Turns(Make((uint)seed, 12, 12, twistiness: 0)));
        var twisty = Enumerable.Range(1, 12).Sum(seed => Turns(Make((uint)seed, 12, 12, twistiness: 100)));

        Assert.True(straight < twisty, $"{straight} bends without twistiness, {twisty} with it");
    }

    [Fact]
    public void Generate_TheCellMiddlesAreOpenFloorAndTheCornerBlocksAreWall()
    {
        var maze = Make(3, corridor: 4);

        for (var cell = 0; cell < maze.Columns * maze.Rows; cell++)
        {
            var (column, row) = maze.CellCenter(cell);
            Assert.False(maze.IsWall((int)column, (int)row));
        }
        for (var row = 0; row < maze.BlockRows; row += maze.Corridor + 1)
            for (var column = 0; column < maze.BlockColumns; column += maze.Corridor + 1)
                Assert.True(maze.IsWall(column, row));
    }

    [Theory]
    [InlineData(2, 5, 50, 3)]
    [InlineData(5, 2, 50, 3)]
    [InlineData(31, 5, 50, 3)]
    [InlineData(5, 5, -1, 3)]
    [InlineData(5, 5, 101, 3)]
    [InlineData(5, 5, 50, 2)]
    [InlineData(5, 5, 50, 13)]
    public void Generate_ParametersOutOfRange_AreRefused(int columns, int rows, int twistiness, int corridor)
        => Assert.Throws<ArgumentOutOfRangeException>(() => MazeGenerator.Generate(1, columns, rows, twistiness, corridor));

    // The shared vector: the TypeScript port (Application.Ui/src/game/maze.spec.ts) checks the same file.

    private sealed record VectorCase(
        uint Seed, int Columns, int Rows, int Twistiness, int Corridor, string EntranceSide, int EntranceCell, int PrizeCell, string[] Walls);

    private static readonly (uint Seed, int Columns, int Rows, int Twistiness, int Corridor)[] VectorInputs =
    [
        (1, 4, 3, 0, 3), (12345, 6, 5, 50, 3), (987654321, 8, 6, 100, 4), (4294967295, 10, 8, 30, 3), (0, 5, 5, 75, 5)
    ];

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    /// <summary>Set MAZE_VECTOR_WRITE=1 to rewrite the vector from the generator (only when the algorithm is changed on purpose, in both languages).</summary>
    [Fact]
    public void SharedVector_IsWhatTheGeneratorMakes()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "maze-vector.json");
        if (Environment.GetEnvironmentVariable("MAZE_VECTOR_WRITE") == "1")
        {
            path = SourceVectorPath();
            File.WriteAllText(path, JsonSerializer.Serialize(VectorInputs.Select(Make), Json) + "\n");
        }

        var expected = JsonSerializer.Deserialize<VectorCase[]>(File.ReadAllText(path), Json)!;

        Assert.Equal(VectorInputs.Select(input => (input.Seed, input.Columns, input.Rows, input.Twistiness, input.Corridor)),
            expected.Select(vector => (vector.Seed, vector.Columns, vector.Rows, vector.Twistiness, vector.Corridor)));
        foreach (var vector in expected)
        {
            var maze = MazeGenerator.Generate(vector.Seed, vector.Columns, vector.Rows, vector.Twistiness, vector.Corridor);
            Assert.Equal(vector.Walls, maze.ToAscii().Split('\n'));
            Assert.Equal(vector.EntranceSide, maze.EntranceSide.ToString());
            Assert.Equal(vector.EntranceCell, maze.EntranceCell);
            Assert.Equal(vector.PrizeCell, maze.PrizeCell);
        }
    }

    private static VectorCase Make((uint Seed, int Columns, int Rows, int Twistiness, int Corridor) input)
    {
        var maze = MazeGenerator.Generate(input.Seed, input.Columns, input.Rows, input.Twistiness, input.Corridor);
        return new VectorCase(input.Seed, input.Columns, input.Rows, input.Twistiness, input.Corridor, maze.EntranceSide.ToString(),
            maze.EntranceCell, maze.PrizeCell, maze.ToAscii().Split('\n'));
    }

    /// <summary>Where the vector lives in the source tree (src/Tests/Shared), found from the test binaries.</summary>
    private static string SourceVectorPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Shared")))
            directory = directory.Parent;
        return Path.Combine(directory!.FullName, "Shared", "maze-vector.json");
    }

    // Helpers that look at a maze from the outside, through IsWall only.

    /// <summary>Steps from the entrance to every cell, found by walking the blocks (-1: not reachable).</summary>
    private static int[] Distances(MazeLayout maze)
    {
        var distance = Enumerable.Repeat(-1, maze.Columns * maze.Rows).ToArray();
        distance[maze.EntranceCell] = 0;
        var queue = new Queue<int>([maze.EntranceCell]);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var next in Neighbors(maze, cell).Where(next => distance[next] < 0))
            {
                distance[next] = distance[cell] + 1;
                queue.Enqueue(next);
            }
        }
        return distance;
    }

    /// <summary>The cells next to this one with no wall between them: the middle block of the shared side is open.</summary>
    private static IEnumerable<int> Neighbors(MazeLayout maze, int cell)
    {
        var pitch = maze.Corridor + 1;
        var column = cell % maze.Columns;
        var row = cell / maze.Columns;
        var middle = maze.Corridor / 2 + 1;
        if (column > 0 && !maze.IsWall(column * pitch, row * pitch + middle)) yield return cell - 1;
        if (column < maze.Columns - 1 && !maze.IsWall((column + 1) * pitch, row * pitch + middle)) yield return cell + 1;
        if (row > 0 && !maze.IsWall(column * pitch + middle, row * pitch)) yield return cell - maze.Columns;
        if (row < maze.Rows - 1 && !maze.IsWall(column * pitch + middle, (row + 1) * pitch)) yield return cell + maze.Columns;
    }

    private static int DeadEnds(MazeLayout maze)
        => Enumerable.Range(0, maze.Columns * maze.Rows).Count(cell => cell != maze.EntranceCell && Neighbors(maze, cell).Count() == 1);

    /// <summary>Cells where the way through bends: two neighbours that are not opposite each other.</summary>
    private static int Turns(MazeLayout maze)
        => Enumerable.Range(0, maze.Columns * maze.Rows).Count(cell =>
        {
            var neighbors = Neighbors(maze, cell).ToArray();
            return neighbors.Length == 2 && neighbors[0] + neighbors[1] != 2 * cell;
        });
}
