using Domain.Game.Entities;
using Domain.Game.Rules;
using Domain.Game.Services;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;
using NSubstitute;

namespace Domain.Game.Tests;

public sealed class ObstacleServiceTests
{
    private readonly PointAxisServices _axes = new();
    private readonly ObstacleService _service;
    private readonly IRoom _room = Substitute.For<IRoom>();

    public ObstacleServiceTests()
    {
        _room.Width = 1920;
        _room.Height = 1080;
        _service = new ObstacleService(_axes, new ShotRangeRules());
    }

    private IPointAxis At(double x, double y)
    {
        var axis = _axes.Create(0, 0);
        axis.X = x;
        axis.Y = y;
        return axis;
    }

    private IObstacle Fire(double fromX, double fromY, double axisX, double axisY, Direction direction = Direction.Angular)
        => _service.Create(
            new TrackableId(Guid.NewGuid()), _room, At(fromX, fromY), new MovementDirection(direction, At(axisX, axisY)),
            15, new TrackableId(Guid.NewGuid()));

    [Theory]
    [InlineData(Direction.Right, 1, 0, 1920, 50)]
    [InlineData(Direction.Left, -1, 0, 0, 50)]
    [InlineData(Direction.Up, 0, -1, 100, 0)]
    [InlineData(Direction.Down, 0, 1, 100, 1080)]
    public void Create_CardinalShot_EndsAtTheEdgeInFrontOfIt(Direction direction, double axisX, double axisY, double endX, double endY)
    {
        var shot = Fire(100, 50, axisX, axisY, direction);

        Assert.Equal(endX, shot.EndPosition.X, 6);
        Assert.Equal(endY, shot.EndPosition.Y, 6);
    }

    [Fact]
    public void Create_ShotAtAnyAngle_EndsOnTheRoomBorder()
    {
        var shot = Fire(0, 0, Math.Cos(Math.PI / 4), Math.Sin(Math.PI / 4));

        Assert.Equal(1080, shot.EndPosition.X, 6);
        Assert.Equal(1080, shot.EndPosition.Y, 6);
    }

    [Fact]
    public void Create_CopiesTheStartPosition()
    {
        var start = At(10, 20);

        var shot = _service.Create(new TrackableId(Guid.NewGuid()), _room, start, new MovementDirection(Direction.Right, At(1, 0)), 15, new TrackableId(Guid.NewGuid()));
        start.X = 999;

        Assert.Equal(10, shot.InitialPosition.X);
        Assert.Equal(10, shot.CurrentPosition.X);
    }

    [Fact]
    public void IsOnEndPosition_BeforeTheEnd_IsFalse()
    {
        var shot = Fire(100, 50, 1, 0, Direction.Right);
        shot.CurrentPosition = At(1500, 50);

        Assert.False(_service.IsOnEndPosition(shot));
    }

    [Theory]
    [InlineData(Direction.Right, 1, 0)]
    [InlineData(Direction.Left, -1, 0)]
    [InlineData(Direction.Up, 0, -1)]
    [InlineData(Direction.Down, 0, 1)]
    public void IsOnEndPosition_AtTheEnd_IsTrueForEveryCardinalDirection(Direction direction, double axisX, double axisY)
    {
        var shot = Fire(960, 540, axisX, axisY, direction);
        shot.CurrentPosition = At(shot.EndPosition.X, shot.EndPosition.Y);

        Assert.True(_service.IsOnEndPosition(shot));
    }

    [Fact]
    public void IsOnEndPosition_ShotAtAnAngle_ExpiresOnceItTravelledFarEnough()
    {
        var shot = Fire(0, 0, Math.Cos(Math.PI / 4), Math.Sin(Math.PI / 4));

        shot.CurrentPosition = At(500, 500);
        Assert.False(_service.IsOnEndPosition(shot));

        shot.CurrentPosition = At(1081, 1081);
        Assert.True(_service.IsOnEndPosition(shot));
    }
}
