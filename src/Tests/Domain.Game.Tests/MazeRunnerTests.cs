using Domain.Game.Entities;
using Domain.Game.Rules;
using Domain.Game.Services;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Entities.Configurations;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Domain.Game.Tests;

public sealed class MazeRunnerTests
{
    private const int HeadSize = 15;

    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly FakeTime _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly IRoom _room = Substitute.For<IRoom>();
    private readonly GameState _state;

    public MazeRunnerTests()
    {
        _room.Guid.Returns(Guid.NewGuid());
        _room.Width = 1920;
        _room.Height = 1080;
        _state = new GameState(_room);
        AddPlayer("ana", 10, 10);
        AddPlayer("bo", 20, 10);
    }

    private static MazeOptions Small(Func<MazeOptions, MazeOptions>? change = null)
    {
        var options = new MazeOptions
        {
            MinIntervalSeconds = 30, MaxIntervalSeconds = 30, DurationSeconds = 100, Columns = 6, Rows = 4, CorridorHeads = 3,
            Twistiness = 40, WallMassLoss = 5, FoodPercent = 0, MinPlayers = 2, PrizeMass = 300, PrizeFoodsCredit = 25,
        };
        return change?.Invoke(options) ?? options;
    }

    private static WorldEventOptions Quiet(MazeOptions maze) => new()
    {
        Lightning = new LightningOptions { Enabled = false },
        Meteors = new MeteorOptions { Enabled = false },
        Turret = new TurretOptions { Enabled = false },
        Zone = new ZoneOptions { Enabled = false },
        FruitShower = new FruitShowerOptions { Enabled = false },
        Bounty = new BountyOptions { Enabled = false },
        Mines = new MineFieldOptions { Enabled = false },
        SugarRush = new SugarRushOptions { Enabled = false },
        Maze = maze,
    };

    private MazeRunner Runner(MazeOptions? options = null, Func<double>? random = null)
        => new(new WorldEventRules(Quiet(options ?? Small()), () => 0.5), new EventSchedule(random ?? (() => 0.5)),
            Options.Create(new SnakeConfiguration { Speed = 15, HeadSize = HeadSize, InitialSnakeSize = 5 }), _time);

    private Player AddPlayer(string name, double x, double y)
    {
        var player = new Player(Guid.NewGuid(), name) { CurrentPosition = new PointAxis { X = x, Y = y }, IsAlive = true };
        _state.Players.Add(player.Guid, player);
        return player;
    }

    private void Pass(MazeRunner runner, double seconds)
    {
        _time.Now = _time.Now.AddSeconds(seconds);
        runner.Update(_state);
    }

    private MazeRunner Risen(MazeOptions? options = null, Func<double>? random = null)
    {
        var runner = Runner(options, random);
        runner.Update(_state);
        Pass(runner, 30);
        return runner;
    }

    private IFood? Prize => _state.Maze is { } maze ? (_state.Foods.TryGetValue(maze.PrizeId, out var food) ? food : null) : null;

    [Fact]
    public void BeforeItsTime_NoMazeStands()
    {
        var runner = Runner();
        runner.Update(_state);
        Pass(runner, 29);

        Assert.Null(_state.Maze);
        Assert.Empty(_state.Foods);
    }

    [Fact]
    public void WhenDue_AMazeRisesWithTheOptionsAndForItsDuration()
    {
        Risen();

        var maze = Assert.IsType<WorldMaze>(_state.Maze);
        Assert.Equal((6, 4, 3, 40), (maze.Columns, maze.Rows, maze.Corridor, maze.Twistiness));
        Assert.Equal(5, maze.WallMassLoss);
        Assert.Equal(HeadSize, maze.Unit);
        Assert.Equal(_time.Now.AddSeconds(100), maze.EndsAt);
    }

    [Fact]
    public void TheWallsAreWhatTheGeneratorMakesFromTheSeed()
    {
        Risen();

        var maze = _state.Maze!;
        var made = MazeGenerator.Generate(maze.Seed, maze.Columns, maze.Rows, maze.Twistiness, maze.Corridor);

        Assert.Equal(made.ToAscii(), Assert.IsType<MazeLayout>(maze.Walls).ToAscii());
    }

    [Fact]
    public void TheMazeStaysAwayFromTheEdgesOfTheRoom()
    {
        foreach (var value in new[] { 0.0, 0.25, 0.999999 })
        {
            _state.Maze = null;
            _state.Foods.Clear();
            var runner = Risen(random: () => value);
            var maze = _state.Maze!;
            var layout = maze.Walls;

            Assert.True(maze.X >= 4 * HeadSize, $"left {maze.X}");
            Assert.True(maze.Y >= 4 * HeadSize, $"top {maze.Y}");
            Assert.True(maze.X + layout.BlockColumns * maze.Unit <= _room.Width - 4 * HeadSize, $"right of {value}");
            Assert.True(maze.Y + layout.BlockRows * maze.Unit <= _room.Height - 4 * HeadSize, $"bottom of {value}");
            _ = runner;
        }
    }

    [Fact]
    public void ThePrizeIsAFoodOfTheMazeKindAtTheFarCellWithTheConfiguredReward()
    {
        Risen(Small(o => o with { PrizeGrantsPerk = "swift", PrizeColor = "#112233" }));

        var maze = _state.Maze!;
        var layout = Assert.IsType<MazeLayout>(maze.Walls);
        var prize = Prize!;
        Assert.Equal(FoodKind.Prize, prize.Kind);
        Assert.Equal(MazeOptions.PrizeId, prize.PrizeId);
        Assert.Equal((300L, 25, "swift"), (prize.Value, prize.FoodsCredit, prize.GrantsPerk));
        Assert.Equal("#112233", prize.Color.FillColor);
        var (column, row) = layout.CellCenter(layout.PrizeCell);
        Assert.Equal(maze.X + column * HeadSize - HeadSize / 2.0, prize.Position.X, 3);
        Assert.Equal(maze.Y + row * HeadSize - HeadSize / 2.0, prize.Position.Y, 3);
        Assert.False(maze.BlocksSquare(prize.Position.X, prize.Position.Y, HeadSize));
    }

    [Fact]
    public void WithAFoodShareOfZero_OnlyThePrizeIsInside()
    {
        Risen(Small(o => o with { FoodPercent = 0 }));

        Assert.Single(_state.Foods);
    }

    [Fact]
    public void WithAFoodShareOf100_EveryCellButTheEntranceAndThePrizeHasAFruit_NoneInAWall()
    {
        Risen(Small(o => o with { FoodPercent = 100 }));

        var maze = _state.Maze!;
        var fruits = _state.Foods.Values.Where(food => food.Kind == FoodKind.Normal).ToArray();
        Assert.Equal(6 * 4 - 2, fruits.Length);
        Assert.All(fruits, food => Assert.False(maze.BlocksSquare(food.Position.X, food.Position.Y, HeadSize)));
    }

    [Fact]
    public void TheMazeGoesWhenItsTimeIsUp_AndTakesThePrizeWithIt()
    {
        var runner = Risen();
        Pass(runner, 99);
        Assert.NotNull(_state.Maze);

        Pass(runner, 2);

        Assert.Null(_state.Maze);
        Assert.Empty(_state.Foods);
    }

    [Fact]
    public void TheMazeGoesAtOnceWhenThePrizeIsTaken_AndFruitsInsideStay()
    {
        var runner = Risen(Small(o => o with { FoodPercent = 100 }));
        var fruits = _state.Foods.Count - 1;
        _state.Foods.Remove(_state.Maze!.PrizeId);

        Pass(runner, 1);

        Assert.Null(_state.Maze);
        Assert.Equal(fruits, _state.Foods.Count);
    }

    [Fact]
    public void AfterOneMazeAnotherComesLater()
    {
        var runner = Risen();
        _state.Foods.Remove(_state.Maze!.PrizeId);
        Pass(runner, 1);
        Assert.Null(_state.Maze);

        Pass(runner, 29);
        Assert.Null(_state.Maze);
        Pass(runner, 2);

        Assert.NotNull(_state.Maze);
    }

    [Fact]
    public void NeedsItsPlayers_ButAnAdministratorCanCallItWithFewer()
    {
        _state.Players.Remove(_state.Players.Keys.First());
        var runner = Runner();
        runner.Update(_state);
        Pass(runner, 60);
        Assert.Null(_state.Maze);

        Assert.True(runner.Trigger(_state));
        Pass(runner, 0);

        Assert.NotNull(_state.Maze);
    }

    [Fact]
    public void ACallIsRefusedWhileAMazeStands_OrWhenItIsOffInTheRoom()
    {
        var runner = Risen();
        Assert.False(runner.Trigger(_state));

        var off = Runner(Small(o => o with { Enabled = false }));
        Assert.False(off.Trigger(_state));
    }

    [Fact]
    public void WhenTheRoomEmpties_TheMazeAndItsPrizeGo()
    {
        var runner = Risen();
        _state.Players.Clear();

        runner.Update(_state);

        Assert.Null(_state.Maze);
        Assert.Empty(_state.Foods);
    }

    [Fact]
    public void SwitchedOff_NothingRises()
    {
        var runner = Runner(Small(o => o with { Enabled = false }));
        runner.Update(_state);
        Pass(runner, 100);

        Assert.Null(_state.Maze);
    }

    [Fact]
    public void ARoomTooSmallForTheSizeAsked_GetsASmallerMaze()
    {
        _room.Width = 700;
        _room.Height = 500;

        Risen(Small(o => o with { Columns = 30, Rows = 30 }));

        var maze = _state.Maze!;
        Assert.InRange(maze.Columns, MazeGenerator.MinCells, 30);
        Assert.True(maze.Columns < 30 && maze.Rows < 30);
        Assert.True(maze.X + maze.Walls.BlockColumns * HeadSize <= 700 - 4 * HeadSize);
        Assert.True(maze.Y + maze.Walls.BlockRows * HeadSize <= 500 - 4 * HeadSize);
    }

    [Fact]
    public void ARoomThatCannotHoldTheSmallestMaze_GetsNone_AndItTriesAgainLater()
    {
        _room.Width = 300;
        _room.Height = 300;

        var runner = Risen();

        Assert.Null(_state.Maze);
        Assert.Empty(_state.Foods);
        Pass(runner, 1);
        Assert.Null(_state.Maze);
    }

    [Fact]
    public void ItDoesNotRiseOnTheHeadOfALivingSnake()
    {
        var player = AddPlayer("cy", 960, 540);
        var random = new Random(7);

        Risen(random: () => random.NextDouble());

        var maze = _state.Maze;
        Assert.NotNull(maze);
        Assert.False(maze!.BlocksSquare(player.CurrentPosition.X, player.CurrentPosition.Y, HeadSize));
        var right = maze.X + maze.Walls.BlockColumns * HeadSize;
        var bottom = maze.Y + maze.Walls.BlockRows * HeadSize;
        Assert.False(player.CurrentPosition.X > maze.X && player.CurrentPosition.X < right && player.CurrentPosition.Y > maze.Y && player.CurrentPosition.Y < bottom);
    }

    [Fact]
    public void IfEveryPlaceHasAHead_NoMazeRises()
    {
        AddPlayer("cy", 960, 540);
        var runner = Risen(random: () => 0.5);

        Assert.Null(_state.Maze);
        _ = runner;
    }

    [Fact]
    public void ADeadSnakeHeadDoesNotCount()
    {
        AddPlayer("cy", 960, 540).IsAlive = false;

        Risen(random: () => 0.5);

        Assert.NotNull(_state.Maze);
    }
}
