using Domain.Game.Entities;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Tests;

/// <summary>A grid of blocks written as text: <c>#</c> is wall. Everything outside the text is open.</summary>
internal sealed class TextWalls(params string[] lines) : IMazeWalls
{
    public int Lookups { get; private set; }
    public int BlockColumns => lines[0].Length;
    public int BlockRows => lines.Length;

    public bool IsWall(int column, int row)
    {
        Lookups++;
        return column >= 0 && row >= 0 && row < lines.Length && column < lines[row].Length && lines[row][column] == '#';
    }
}

/// <summary>What a maze does to heads and shots inside <see cref="Domain.Game.Services.GameStateService"/>: walls hold a head back, cost it mass, never kill, and stop shots.</summary>
public sealed partial class GameStateServiceTests
{
    // Blocks of one head size from (400, 400). Rows 1 and 2 (y 415 to 445) are a corridor; the wall at its right end starts at x 535.
    private static readonly string[] Corridor =
    [
        "##########",
        "#........#",
        "#........#",
        "##########"
    ];

    private TextWalls PutMaze(long wallMassLoss = 3, string[]? lines = null)
    {
        var walls = new TextWalls(lines ?? Corridor);
        _state.Maze = new WorldMaze(400, 400, 3, 1, 3, 50, 1, HeadSize, _clock.GetUtcNow().AddMinutes(4), walls, Guid.NewGuid(), wallMassLoss);
        return walls;
    }

    private async Task MoveRight(Player player, int ticks)
    {
        for (var tick = 0; tick < ticks; tick++) await _service.MovePlayer(_state, player);
    }

    [Fact]
    public async Task MovePlayer_AWallHoldsTheHeadBack()
    {
        PutMaze();
        var player = AddPlayer("a", 500, 415, score: 10);

        await MoveRight(player, 60);

        Assert.InRange(player.CurrentPosition.X, 516, 520);
        Assert.True(player.IsAlive);
    }

    [Fact]
    public async Task MovePlayer_WithoutAMaze_TheHeadGoesWhereItWants()
    {
        var player = AddPlayer("a", 500, 415, score: 10);

        await MoveRight(player, 60);

        Assert.True(player.CurrentPosition.X > 520);
    }

    [Fact]
    public async Task MovePlayer_TouchingAWallCostsMassAndItFallsAsFood()
    {
        PutMaze(wallMassLoss: 3);
        var player = AddPlayer("a", 500, 415, score: 10);

        await MoveRight(player, 60);

        Assert.Equal(7, player.Score);
        Assert.Equal(3, _state.Foods.Values.Sum(food => food.Value));
        Assert.Contains(_state.Hits, hit => hit.Kind == DamageKind.Lost && hit.TargetGuid == player.Guid && hit.Damage == 3);
    }

    [Fact]
    public async Task MovePlayer_PushingOnAWall_CostsMassOnlyOnceInAWhile()
    {
        PutMaze(wallMassLoss: 3);
        var player = AddPlayer("a", 500, 415, score: 20);

        await MoveRight(player, 120);
        Assert.Equal(17, player.Score);

        _clock.Advance(TimeSpan.FromMilliseconds(600));
        await MoveRight(player, 5);
        Assert.Equal(14, player.Score);
    }

    [Fact]
    public async Task MovePlayer_AWallNeverKills_ASnakeWithNoMassLosesNothingAndLives()
    {
        PutMaze(wallMassLoss: 50);
        var player = AddPlayer("a", 500, 415, score: 4);

        foreach (var _ in Enumerable.Range(0, 10))
        {
            await MoveRight(player, 60);
            _clock.Advance(TimeSpan.FromSeconds(1));
        }

        Assert.True(player.IsAlive);
        Assert.Equal(0, player.Score);
    }

    [Fact]
    public async Task MovePlayer_ALongSnakeSurvivesAnyNumberOfWallHits()
    {
        PutMaze(wallMassLoss: 2);
        var player = AddPlayer("a", 500, 415, length: 30, score: 25);

        foreach (var _ in Enumerable.Range(0, 100))
        {
            await MoveRight(player, 5);
            _clock.Advance(TimeSpan.FromSeconds(1));
        }

        Assert.True(player.IsAlive);
        Assert.True(player.Score >= 0);
    }

    [Fact]
    public async Task MovePlayer_AWallWithNoMassLoss_JustHoldsTheHead()
    {
        PutMaze(wallMassLoss: 0);
        var player = AddPlayer("a", 500, 415, score: 10);

        await MoveRight(player, 60);

        Assert.InRange(player.CurrentPosition.X, 516, 520);
        Assert.Equal(10, player.Score);
        Assert.Empty(_state.Hits);
    }

    [Fact]
    public async Task MovePlayer_AHeadSlidesAlongAWallInsteadOfSticking()
    {
        PutMaze();
        var player = AddPlayer("a", 515, 415, score: 10);
        player.Direction = new MovementDirection(Direction.Angular, At(1, 0.5));

        await MoveRight(player, 30);

        Assert.InRange(player.CurrentPosition.X, 516, 520);
        Assert.True(player.CurrentPosition.Y > 420, $"y {player.CurrentPosition.Y}");
        Assert.True(player.CurrentPosition.Y <= 430, $"y {player.CurrentPosition.Y}");
    }

    [Fact]
    public async Task MovePlayer_AHeadAlreadyInsideAWall_MayWalkOutWithoutPaying()
    {
        PutMaze();
        var player = AddPlayer("a", 540, 415, score: 10);

        await MoveRight(player, 10);

        Assert.True(player.CurrentPosition.X > 540);
        Assert.Equal(10, player.Score);
    }

    [Fact]
    public async Task MovePlayer_ABoostingHeadStillStopsAtTheWall()
    {
        PutMaze();
        var player = AddPlayer("a", 500, 415, score: 100);
        player.IsBoosting = true;

        await MoveRight(player, 60);

        Assert.True(player.CurrentPosition.X <= 520 + 1e-6);
    }

    [Fact]
    public async Task MoveObstacles_AShotThatReachesAWallVanishes()
    {
        PutMaze();
        var shot = AddShot(AddPlayer("a", 420, 600), 500, 425);

        for (var tick = 0; tick < 12; tick++) await _service.MoveObstacles(_state);

        Assert.DoesNotContain(shot.Guid, _state.Obstacles.Keys);
    }

    [Fact]
    public async Task MoveObstacles_AShotInOpenFloorFliesOn()
    {
        PutMaze();
        var shot = AddShot(AddPlayer("a", 420, 600), 420, 425);

        for (var tick = 0; tick < 3; tick++) await _service.MoveObstacles(_state);

        Assert.Contains(shot.Guid, _state.Obstacles.Keys);
        Assert.True(shot.CurrentPosition.X > 420);
    }

    [Fact]
    public async Task MoveObstacles_AFastShotCannotJumpOverAWall()
    {
        PutMaze();
        var shot = AddShot(AddPlayer("a", 420, 600), 500, 425);
        shot.SpeedFactor = 20;

        await _service.MoveObstacles(_state);

        Assert.DoesNotContain(shot.Guid, _state.Obstacles.Keys);
    }

    [Fact]
    public async Task MoveObstacles_AShotOutsideTheMazeIsLeftAlone()
    {
        PutMaze();
        var shot = AddShot(AddPlayer("a", 420, 900), 100, 900);

        for (var tick = 0; tick < 5; tick++) await _service.MoveObstacles(_state);

        Assert.Contains(shot.Guid, _state.Obstacles.Keys);
    }

    [Fact]
    public async Task MovePlayer_AWallCheckIsAFewLookups_NotAScanOfTheMaze()
    {
        var walls = PutMaze();
        var player = AddPlayer("a", 450, 415, score: 10);

        await _service.MovePlayer(_state, player);

        Assert.InRange(walls.Lookups, 1, 12);
    }
}
