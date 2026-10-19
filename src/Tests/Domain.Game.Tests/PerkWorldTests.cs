using Domain.Game.Entities;
using Domain.Game.Rules;
using Domain.Game.Services;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;
using NSubstitute;

namespace Domain.Game.Tests;

public sealed class PerkWorldTests
{
    private const string AskTwice = "function onTick(room) { room.state.n = (room.state.n || 0) + 1; return room.state.n === 2 ? { dropPrize: 'prize' } : {}; }";

    private readonly GameState _state;
    private readonly PerkWorld _world;
    private readonly PrizeSpawner _prizes;

    public PerkWorldTests()
    {
        var room = Substitute.For<IRoom>();
        room.Guid.Returns(Guid.NewGuid());
        room.Width = 1920;
        room.Height = 1080;
        _state = new GameState(room);
        var perks = new PerkRules(new PerkOptions
        {
            Perks = [new() { Id = "scripted", Name = "Scripted", FoodsRequired = 1, Script = AskTwice, Rooms = [room.Guid.ToString()] }]
        });
        _prizes = new PrizeSpawner(new PrizeRules(new PrizeOptions()), TimeProvider.System);
        _world = new PerkWorld(perks, _prizes, TimeProvider.System);
    }

    private void AddPlayer()
    {
        var player = new Player(Guid.NewGuid(), "p");
        _state.Players.Add(player.Guid, player);
    }

    private int PrizeCount() => _state.Foods.Values.Count(food => food.Kind == FoodKind.Prize);

    [Fact]
    public void Update_AScriptThatAsksForAPrize_GetsItOnTheMap()
    {
        AddPlayer();

        _world.Update(_state);
        Assert.Equal(0, PrizeCount());
        _world.Update(_state);

        Assert.Equal(1, PrizeCount());
    }

    [Fact]
    public void Update_ARoomThatEmptiedForgetsWhatItsScriptsRemembered()
    {
        AddPlayer();
        _world.Update(_state);
        foreach (var id in _state.Players.Keys.ToArray()) _state.Players.Remove(id);
        _world.Update(_state);
        AddPlayer();

        _world.Update(_state);

        Assert.Equal(0, PrizeCount());
    }
}
