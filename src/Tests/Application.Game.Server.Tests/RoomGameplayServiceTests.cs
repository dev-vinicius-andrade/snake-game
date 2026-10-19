using Application.Game.Server.Services;
using Domain.Game.Rules;
using Domain.Game.Services;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using NSubstitute;

namespace Application.Game.Server.Tests;

public sealed class RoomGameplayServiceTests
{
    private readonly IRoomsService _rooms = Substitute.For<IRoomsService>();
    private readonly IGameStateService _gameStates = Substitute.For<IGameStateService>();
    private readonly ITrackableIdGeneratorService _ids = Substitute.For<ITrackableIdGeneratorService>();
    private readonly IRoom _room = Substitute.For<IRoom>();
    private readonly IGameState _state = Substitute.For<IGameState>();
    private readonly IEventTriggers _triggers = Substitute.For<IEventTriggers>();
    private readonly RoomGameplayService _service;
    private readonly Guid _roomId = Guid.NewGuid();

    public RoomGameplayServiceTests()
    {
        _room.Guid.Returns(_roomId);
        _rooms.TryGetRoom(Arg.Any<Guid>()).Returns((IRoom?)null);
        _rooms.TryGetRoom(_roomId).Returns(_room);
        _ids.Generate(_roomId).Returns(call => new FakeId(_roomId));
        _gameStates.GetGameState(Arg.Any<ITrackableId>()).Returns(_state);
        _service = new RoomGameplayService(_rooms, _gameStates, _ids, _triggers);
    }

    private sealed record FakeId(Guid Guid) : ITrackableId;

    [Fact]
    public void Set_ReplacesWhatTheRoomPicked()
    {
        var picked = new RoomGameplay { Turret = FeaturePreset.Off };

        Assert.True(_service.Set(_roomId, picked));

        Assert.Equal(picked, _room.Gameplay);
    }

    [Fact]
    public void Set_ARoomThatIsNotThere_IsRefused()
    {
        Assert.False(_service.Set(Guid.NewGuid(), new RoomGameplay()));
    }

    [Theory]
    [InlineData("lightning")]
    [InlineData("meteors")]
    [InlineData("turret")]
    [InlineData("zone")]
    public void Trigger_StartsTheEventTheAdminNamed(string kind)
    {
        _triggers.Trigger(_state, kind).Returns(TriggerResult.Started);

        Assert.Equal(TriggerResult.Started, _service.Trigger(_roomId, kind));
    }

    [Fact]
    public void Trigger_ARoomThatIsNotThere_SaysSo()
    {
        Assert.Equal(TriggerResult.RoomNotFound, _service.Trigger(Guid.NewGuid(), "zone"));
    }

    [Fact]
    public void Trigger_AnEventItDoesNotKnow_SaysSoAndStartsNothing()
    {
        Assert.Equal(TriggerResult.UnknownEvent, _service.Trigger(_roomId, "earthquake"));
        _triggers.DidNotReceiveWithAnyArgs().Trigger(default!, default!);
    }

    [Fact]
    public void Trigger_PassesOnWhyItDidNotStart()
    {
        _triggers.Trigger(_state, "zone").Returns(TriggerResult.NotNow);

        Assert.Equal(TriggerResult.NotNow, _service.Trigger(_roomId, "zone"));
    }

    [Fact]
    public void Trigger_TheNamesAreNotCaseSensitive()
    {
        _triggers.Trigger(_state, "turret").Returns(TriggerResult.Started);

        Assert.Equal(TriggerResult.Started, _service.Trigger(_roomId, "Turret"));
    }
}

public sealed class EventTriggersTests
{
    [Fact]
    public void Trigger_LetsTheRunnerOfTheEventStartIt_AndSaysWhetherItDid()
    {
        var gameStates = Substitute.For<IGameStateService>();
        var rules = new WorldEventRules(new WorldEventOptions());
        var triggers = new EventTriggers(new WorldEventRunner(rules, gameStates, TimeProvider.System), new TurretRunner(rules, gameStates, TimeProvider.System), new ZoneRunner(rules, gameStates, TimeProvider.System));
        var room = Substitute.For<IRoom>();
        room.Guid.Returns(Guid.NewGuid());
        var state = Substitute.For<IGameState>();
        state.Room.Returns(room);
        state.Players.Returns(new Dictionary<Guid, IPlayer>());

        // Nobody is in the room: nothing can be started in it.
        Assert.Equal(TriggerResult.NotNow, triggers.Trigger(state, "zone"));
        Assert.Equal(TriggerResult.NotNow, triggers.Trigger(state, "turret"));
        Assert.Equal(TriggerResult.NotNow, triggers.Trigger(state, "lightning"));
        Assert.Equal(TriggerResult.NotNow, triggers.Trigger(state, "meteors"));
    }
}
