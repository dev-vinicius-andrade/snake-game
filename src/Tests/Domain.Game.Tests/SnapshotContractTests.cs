using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Game.Entities;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;
using NSubstitute;
using Xunit.Abstractions;
using Color = Domain.Game.Entities.Color;

namespace Domain.Game.Tests;

/// <summary>
/// Pins the JSON the Vue client parses (client/types/game). SignalR serializes hub arguments by runtime type
/// with the options the game server registers, copied here. If this test breaks, update the client types and parser.
/// </summary>
public sealed class SnapshotContractTests(ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions ServerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private static PointAxis Point(double x, double y) => new() { X = x, Y = y, Z = 0 };

    private JsonElement SerializeSampleState()
    {
        var room = Substitute.For<IRoom>();
        room.Guid.Returns(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        room.Width = 1920;
        room.Height = 1080;
        var state = new GameState(room);
        var path = new List<IPointAxis> { Point(485, 500), Point(500, 500) };
        var player = new Player(Guid.Parse("22222222-2222-2222-2222-222222222222"), "ana")
        {
            Id = "connection-1",
            CurrentPosition = path[^1],
            CurrentDirection = Direction.Right,
            Direction = new MovementDirection(Direction.Right, Point(1, 0)),
            BackgroundColor = new Color("#ff0000", "#aa0000"),
            Path = path,
            SegmentHealth = new List<double> { 0.5, 1.0 },
            Score = 7,
            IsBoosting = true,
            Heading = 0.5,
            TargetHeading = 1.0
        };
        state.Players.Add(player.Guid, player);
        var food = new Food(Guid.Parse("33333333-3333-3333-3333-333333333333"))
        {
            Position = Point(10, 20),
            Color = new Color("#00ff00", "#00aa00"),
            Value = 3
        };
        state.Foods.Add(food.Guid, food);
        var shot = new Obstacle(Guid.Parse("44444444-4444-4444-4444-444444444444"), 15, room, player)
        {
            InitialPosition = Point(1, 1),
            CurrentPosition = Point(30, 40),
            EndPosition = Point(1920, 0),
            Direction = new MovementDirection(Direction.Right, Point(1, 0))
        };
        state.Obstacles.Add(shot.Guid, shot);

        var json = JsonSerializer.Serialize(state, state.GetType(), ServerOptions);
        output.WriteLine(json);
        return JsonDocument.Parse(json).RootElement;
    }

    [Fact]
    public void GameState_HasRoomPlayersFoodsObstacles()
    {
        var root = SerializeSampleState();

        Assert.Equal(1920, root.GetProperty("room").GetProperty("width").GetInt32());
        Assert.Equal(1080, root.GetProperty("room").GetProperty("height").GetInt32());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("players").ValueKind);
        Assert.Equal(JsonValueKind.Object, root.GetProperty("foods").ValueKind);
        Assert.Equal(JsonValueKind.Object, root.GetProperty("obstacles").ValueKind);
    }

    [Fact]
    public void Player_ExposesWhatTheClientDraws()
    {
        var player = SerializeSampleState().GetProperty("players").EnumerateObject().Single().Value;

        Assert.Equal("ana", player.GetProperty("name").GetString());
        Assert.Equal("22222222-2222-2222-2222-222222222222", player.GetProperty("guid").GetString());
        Assert.Equal("connection-1", player.GetProperty("id").GetString());
        Assert.True(player.GetProperty("isAlive").GetBoolean());
        Assert.True(player.GetProperty("isBoosting").GetBoolean());
        Assert.Equal(7, player.GetProperty("score").GetInt64());
        Assert.Equal(2, player.GetProperty("path").GetArrayLength());
        Assert.Equal(500, player.GetProperty("path")[1].GetProperty("x").GetDouble());
        Assert.Equal(0.5, player.GetProperty("segmentHealth")[0].GetDouble());
        Assert.Equal("#ff0000", player.GetProperty("backgroundColor").GetProperty("fillColor").GetString());
        Assert.Equal("right", player.GetProperty("currentDirection").GetString());
        Assert.Equal(0.5, player.GetProperty("heading").GetDouble());
        Assert.Equal(1.0, player.GetProperty("targetHeading").GetDouble());
    }

    [Fact]
    public void Food_ExposesPositionColorAndValue()
    {
        var food = SerializeSampleState().GetProperty("foods").EnumerateObject().Single().Value;

        Assert.Equal(10, food.GetProperty("position").GetProperty("x").GetDouble());
        Assert.Equal("#00ff00", food.GetProperty("color").GetProperty("fillColor").GetString());
        Assert.Equal(3, food.GetProperty("value").GetInt64());
    }

    [Fact]
    public void Shot_ExposesCurrentPositionAndSize()
    {
        var shot = SerializeSampleState().GetProperty("obstacles").EnumerateObject().Single().Value;

        Assert.Equal(30, shot.GetProperty("currentPosition").GetProperty("x").GetDouble());
        Assert.Equal(15, shot.GetProperty("size").GetInt64());
    }
}
