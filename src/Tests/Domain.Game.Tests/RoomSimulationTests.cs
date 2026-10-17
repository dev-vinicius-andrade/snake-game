using Domain.Game.Simulation;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using NSubstitute;

namespace Domain.Game.Tests;

public sealed class RoomSimulationTests
{
    private readonly IGameStateService _gameStateService = Substitute.For<IGameStateService>();
    private readonly IRoomInputQueue _inputQueue = Substitute.For<IRoomInputQueue>();
    private readonly RoomSimulation _simulation;

    public RoomSimulationTests() => _simulation = new RoomSimulation(_gameStateService, _inputQueue);

    [Fact]
    public async Task StepAsync_Always_DrainsInputBeforeAnythingMoves()
    {
        var calls = new List<string>();
        var player = Player();
        var state = StateWith(player);
        _inputQueue.When(queue => queue.DrainAsync(state, Arg.Any<CancellationToken>())).Do(_ => calls.Add("drain"));
        _gameStateService.When(service => service.MoveObstacles(state)).Do(_ => calls.Add("obstacles"));
        _gameStateService.When(service => service.MovePlayer(state, player)).Do(_ => calls.Add("move"));

        await _simulation.StepAsync(state);

        Assert.Equal(new[] { "drain", "obstacles", "move" }, calls);
    }

    [Fact]
    public async Task StepAsync_PlayerJoinsDuringDrain_IsSimulatedInSameStep()
    {
        var state = StateWith();
        var joiner = Player();
        _inputQueue.When(queue => queue.DrainAsync(state, Arg.Any<CancellationToken>()))
            .Do(_ => state.Players.Returns(new Dictionary<Guid, IPlayer> { [Guid.NewGuid()] = joiner }));

        await _simulation.StepAsync(state);

        await _gameStateService.Received(1).MovePlayer(state, joiner);
    }

    private static IPlayer Player(bool isAlive = true)
    {
        var player = Substitute.For<IPlayer>();
        player.IsAlive = isAlive;
        return player;
    }

    private static IGameState StateWith(params IPlayer[] players)
    {
        var state = Substitute.For<IGameState>();
        state.Players.Returns(players.ToDictionary(_ => Guid.NewGuid()));
        return state;
    }

    [Fact]
    public async Task StepAsync_ManyPlayers_MovesObstaclesExactlyOnce()
    {
        var state = StateWith(Player(), Player(), Player());

        await _simulation.StepAsync(state);

        await _gameStateService.Received(1).MoveObstacles(state);
    }

    [Fact]
    public async Task StepAsync_ManyPlayers_MovesEachPlayerOnce()
    {
        var players = new[] { Player(), Player(), Player() };
        var state = StateWith(players);

        await _simulation.StepAsync(state);

        foreach (var player in players)
            await _gameStateService.Received(1).MovePlayer(state, player);
    }

    [Fact]
    public async Task StepAsync_OnePlayer_RunsMoveThenFoodThenObstacle()
    {
        var calls = new List<string>();
        var player = Player();
        var state = StateWith(player);
        _gameStateService.When(service => service.MoveObstacles(state)).Do(_ => calls.Add("obstacles"));
        _gameStateService.When(service => service.MovePlayer(state, player)).Do(_ => calls.Add("move"));
        _gameStateService.When(service => service.CheckFoodCollision(state, player)).Do(_ => calls.Add("food"));
        _gameStateService.When(service => service.CheckObstacleCollision(state, player)).Do(_ => calls.Add("hit"));

        await _simulation.StepAsync(state);

        Assert.Equal(new[] { "obstacles", "move", "food", "hit" }, calls);
    }

    [Fact]
    public async Task StepAsync_DeadPlayer_IsSkipped()
    {
        var dead = Player(isAlive: false);
        var state = StateWith(dead, Player());

        await _simulation.StepAsync(state);

        await _gameStateService.DidNotReceive().MovePlayer(state, dead);
    }

    [Fact]
    public async Task StepAsync_PlayerDiesInCollision_ReportedAsDied()
    {
        var victim = Player();
        var survivor = Player();
        var state = StateWith(victim, survivor);
        _gameStateService.When(service => service.CheckObstacleCollision(state, victim)).Do(_ => victim.IsAlive = false);

        var result = await _simulation.StepAsync(state);

        Assert.Equal(new[] { victim }, result.DiedPlayers);
    }

    [Fact]
    public async Task StepAsync_PlayerDies_IsNotDisconnectedBySimulation()
    {
        var victim = Player();
        var state = StateWith(victim);
        _gameStateService.When(service => service.CheckObstacleCollision(state, victim)).Do(_ => victim.IsAlive = false);

        await _simulation.StepAsync(state);

        await _gameStateService.DidNotReceiveWithAnyArgs().DisconnectPlayer(default!, default!);
    }

    [Fact]
    public async Task StepAsync_NoOneDies_ReportsNoDeaths()
    {
        var result = await _simulation.StepAsync(StateWith(Player(), Player()));

        Assert.Empty(result.DiedPlayers);
    }

    [Fact]
    public async Task StepAsync_CancelledToken_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => _simulation.StepAsync(StateWith(Player()), cancellation.Token));
    }

    [Fact]
    public async Task StepAsync_NullState_Throws()
        => await Assert.ThrowsAsync<ArgumentNullException>(() => _simulation.StepAsync(null!));
}
