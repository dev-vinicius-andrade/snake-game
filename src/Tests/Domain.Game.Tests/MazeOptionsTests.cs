using System.Text.Json;
using Domain.Game.Rules;
using NSubstitute;

namespace Domain.Game.Tests;

public sealed class MazeOptionsTests
{
    private static WorldEventOptions With(Func<MazeOptions, MazeOptions> change) => new() { Maze = change(new MazeOptions()) };

    [Fact]
    public void Defaults_AreOnAndValid()
    {
        Assert.True(GameplayConfig.Defaults.WorldEvents.Maze.Enabled);
        GameplayConfig.Defaults.EnsureValid();
    }

    [Fact]
    public void Defaults_AGapIsWideEnoughForALongSnakeToTurn()
        => Assert.True(new MazeOptions().CorridorHeads >= MazeGenerator.MinCorridor);

    [Fact]
    public void Json_AConfigWithoutAMazeGetsTheDefaults()
    {
        const string old = "{\"worldEvents\":{\"enabled\":true,\"zone\":{\"enabled\":true}}}";

        var config = JsonSerializer.Deserialize<GameplayConfig>(old, GameplayConfig.Json)!;

        Assert.Equal(new MazeOptions().Columns, config.WorldEvents.Maze.Columns);
        Assert.True(config.WorldEvents.Maze.Enabled);
        config.EnsureValid();
    }

    [Fact]
    public void Json_TheMazeRoundTrips()
    {
        var maze = new MazeOptions { Columns = 9, Twistiness = 20, WallMassLoss = 7, PrizeGrantsPerk = "swift", Rooms = ["a"] };

        var json = JsonSerializer.Serialize(new GameplayConfig { WorldEvents = new WorldEventOptions { Maze = maze } }, GameplayConfig.Json);
        var back = JsonSerializer.Deserialize<GameplayConfig>(json, GameplayConfig.Json)!.WorldEvents.Maze;

        Assert.Contains("\"maze\"", json);
        Assert.Equal((9, 20, 7L, "swift"), (back.Columns, back.Twistiness, back.WallMassLoss, back.PrizeGrantsPerk));
        Assert.Equal(["a"], back.Rooms);
    }

    public static TheoryData<string, Func<MazeOptions, MazeOptions>> Refused => new()
    {
        { "no interval", m => m with { MinIntervalSeconds = 0 } },
        { "inverted interval", m => m with { MinIntervalSeconds = 50, MaxIntervalSeconds = 10 } },
        { "no players", m => m with { MinPlayers = 0 } },
        { "blank room", m => m with { Rooms = [" "] } },
        { "too short", m => m with { DurationSeconds = 9 } },
        { "too long", m => m with { DurationSeconds = 3601 } },
        { "too few cells across", m => m with { Columns = 2 } },
        { "too many cells across", m => m with { Columns = 31 } },
        { "too few cells down", m => m with { Rows = 2 } },
        { "too many cells down", m => m with { Rows = 31 } },
        { "corridor too narrow", m => m with { CorridorHeads = 2 } },
        { "corridor too wide", m => m with { CorridorHeads = 13 } },
        { "twistiness below", m => m with { Twistiness = -1 } },
        { "twistiness above", m => m with { Twistiness = 101 } },
        { "negative wall loss", m => m with { WallMassLoss = -1 } },
        { "huge wall loss", m => m with { WallMassLoss = 1001 } },
        { "food share above", m => m with { FoodPercent = 101 } },
        { "food share below", m => m with { FoodPercent = -1 } },
        { "worthless prize", m => m with { PrizeMass = 0 } },
        { "huge prize", m => m with { PrizeMass = 10001 } },
        { "foods credit", m => m with { PrizeFoodsCredit = 1001 } },
        { "colour", m => m with { PrizeColor = "gold" } },
    };

    [Theory]
    [MemberData(nameof(Refused))]
    public void Rules_RefuseOptionsThatCannotWork(string name, Func<MazeOptions, MazeOptions> change)
    {
        Assert.NotNull(name);
        Assert.ThrowsAny<ArgumentException>(() => new WorldEventRules(With(change)));
    }

    [Fact]
    public void EnsureValid_ThePrizeCanOnlyGiveAPerkThatExists()
    {
        var config = GameplayConfig.Defaults with { WorldEvents = With(m => m with { PrizeGrantsPerk = "nope" }) };

        var error = Assert.Throws<ArgumentException>(config.EnsureValid);

        Assert.Contains("nope", error.Message);
    }

    [Fact]
    public void EnsureValid_ThePrizeMayGiveAPerkThatExists()
    {
        var perk = GameplayConfig.Defaults.Perks.Perks[0].Id;
        var config = GameplayConfig.Defaults with { WorldEvents = With(m => m with { PrizeGrantsPerk = perk }) };

        config.EnsureValid();
    }

    [Fact]
    public void EnsureValid_NoOtherPrizeMayUseTheMazePrizeId()
    {
        var config = GameplayConfig.Defaults with
        {
            Prize = GameplayConfig.Defaults.Prize with { Prizes = [new PrizeDefinition { Id = MazeOptions.PrizeId, Name = "x" }] }
        };

        Assert.Throws<ArgumentException>(config.EnsureValid);
    }

    [Fact]
    public void AppliesTo_FollowsTheSwitchAndTheRooms()
    {
        var room = NSubstitute.Substitute.For<Library.Commons.Game.Domain.Interfaces.Entities.IRoom>();
        room.Guid.Returns(Guid.NewGuid());
        room.Name = "arena";

        Assert.True(new MazeOptions().AppliesTo(room));
        Assert.False(new MazeOptions { Enabled = false }.AppliesTo(room));
        Assert.False(new MazeOptions { Rooms = ["other"] }.AppliesTo(room));
        Assert.False(new MazeOptions { Rooms = ["arena"], ExcludedRooms = ["arena"] }.AppliesTo(room));
    }
}
