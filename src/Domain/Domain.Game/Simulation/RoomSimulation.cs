using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;

namespace Domain.Game.Simulation;

/// <summary>Outcome of one simulation step.</summary>
public sealed record SimulationStepResult(IReadOnlyList<IPlayer> DiedPlayers);

/// <summary>
/// One fixed step of one room. Must be called from a single loop per room, never concurrently
/// for the same room. Removing dead players is left to the caller so it can notify them first.
/// </summary>
public sealed class RoomSimulation
{
    private readonly IGameStateService _gameStateService;
    private readonly IRoomInputQueue _inputQueue;

    public RoomSimulation(IGameStateService gameStateService, IRoomInputQueue inputQueue)
    {
        ArgumentNullException.ThrowIfNull(gameStateService);
        ArgumentNullException.ThrowIfNull(inputQueue);
        _gameStateService = gameStateService;
        _inputQueue = inputQueue;
    }

    public async Task<SimulationStepResult> StepAsync(IGameState gameState, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameState);

        await _inputQueue.DrainAsync(gameState, cancellationToken);
        await _gameStateService.MoveObstacles(gameState);
        var livingPlayers = SnapshotLivingPlayers(gameState);
        await MovePlayersAsync(gameState, livingPlayers, cancellationToken);
        await ResolvePickupsAndShotsAsync(gameState, livingPlayers, cancellationToken);
        await _gameStateService.ResolveSnakeCollisions(gameState);
        return new SimulationStepResult(SelectDied(livingPlayers));
    }

    private static IPlayer[] SnapshotLivingPlayers(IGameState gameState)
        => gameState.Players.Values.Where(player => player.IsAlive).ToArray();

    private static IReadOnlyList<IPlayer> SelectDied(IEnumerable<IPlayer> players)
        => players.Where(player => !player.IsAlive).ToArray();

    private async Task MovePlayersAsync(IGameState gameState, IEnumerable<IPlayer> players, CancellationToken cancellationToken)
    {
        foreach (var player in players)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _gameStateService.MovePlayer(gameState, player);
        }
    }

    private async Task ResolvePickupsAndShotsAsync(IGameState gameState, IEnumerable<IPlayer> players, CancellationToken cancellationToken)
    {
        foreach (var player in players)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await _gameStateService.CheckFoodCollision(gameState, player);
            await _gameStateService.CheckObstacleCollision(gameState, player);
        }
    }
}
