using System.Collections.Concurrent;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;

namespace Domain.Game.Simulation;

/// <summary>Per-room mailbox. Any thread enqueues, only the room's simulation loop drains.</summary>
public interface IRoomInputQueue
{
    void Enqueue(Guid roomId, IPlayerCommand command);

    IReadOnlyCollection<Guid> RoomsWithPendingCommands();

    /// <summary>Runs, in order, the commands queued for the room before this call. Later ones wait for the next drain.</summary>
    Task DrainAsync(IGameState gameState, CancellationToken cancellationToken = default);
}

public sealed class RoomInputQueue : IRoomInputQueue
{
    public const int DefaultMaxCommandsPerDrain = 256;

    private readonly IGameStateService _gameStateService;
    private readonly int _maxCommandsPerDrain;
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<IPlayerCommand>> _queues = new();

    public RoomInputQueue(IGameStateService gameStateService, int maxCommandsPerDrain = DefaultMaxCommandsPerDrain)
    {
        ArgumentNullException.ThrowIfNull(gameStateService);
        if (maxCommandsPerDrain < 1)
            throw new ArgumentOutOfRangeException(nameof(maxCommandsPerDrain), maxCommandsPerDrain, "Must be at least 1.");

        _gameStateService = gameStateService;
        _maxCommandsPerDrain = maxCommandsPerDrain;
    }

    public void Enqueue(Guid roomId, IPlayerCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _queues.GetOrAdd(roomId, _ => new ConcurrentQueue<IPlayerCommand>()).Enqueue(command);
    }

    public IReadOnlyCollection<Guid> RoomsWithPendingCommands()
        => _queues.Where(entry => !entry.Value.IsEmpty).Select(entry => entry.Key).ToArray();

    public async Task DrainAsync(IGameState gameState, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(gameState);

        var context = new CommandContext(gameState, _gameStateService);
        foreach (var command in TakeSnapshot(gameState.Room.Guid))
            await command.ExecuteAsync(context, cancellationToken);
    }

    private IReadOnlyList<IPlayerCommand> TakeSnapshot(Guid roomId)
    {
        if (!_queues.TryGetValue(roomId, out var queue)) return Array.Empty<IPlayerCommand>();

        var count = Math.Min(queue.Count, _maxCommandsPerDrain);
        return Enumerable.Range(0, count)
            .Select(_ => queue.TryDequeue(out var command) ? command : null)
            .OfType<IPlayerCommand>()
            .ToArray();
    }
}
