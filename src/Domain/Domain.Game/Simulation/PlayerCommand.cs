using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;

namespace Domain.Game.Simulation;

/// <summary>What a command may touch while it runs inside the room's simulation loop.</summary>
public sealed record CommandContext(IGameState GameState, IGameStateService GameStateService);

/// <summary>An input that changes room state. Only the room's simulation loop executes it.</summary>
public interface IPlayerCommand
{
    Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken);

    /// <summary>Releases anyone awaiting the result when the command will never run.</summary>
    void Abandon();
}

/// <summary>
/// Base for commands whose caller may await the outcome through <see cref="Completion"/>.
/// A failing command faults its own completion and never breaks the simulation step.
/// </summary>
public abstract class PlayerCommand<TResult> : IPlayerCommand
{
    private readonly TaskCompletionSource<TResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<TResult> Completion => _completion.Task;

    public async Task ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
    {
        try
        {
            _completion.TrySetResult(await RunAsync(context, cancellationToken));
        }
        catch (OperationCanceledException)
        {
            _completion.TrySetCanceled(cancellationToken);
            throw;
        }
        catch (Exception exception)
        {
            _completion.TrySetException(exception);
        }
    }

    public void Abandon() => _completion.TrySetCanceled();

    protected abstract Task<TResult> RunAsync(CommandContext context, CancellationToken cancellationToken);

    protected static IPlayer? FindByConnection(IGameState gameState, string connectionId)
        => gameState.Players.Values.FirstOrDefault(player => player.Id == connectionId);
}
