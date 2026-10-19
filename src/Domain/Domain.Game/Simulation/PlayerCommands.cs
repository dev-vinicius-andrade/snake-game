using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Simulation;

public sealed class ChangeDirectionCommand(string connectionId, Direction? direction) : PlayerCommand<bool>, ICoalescableCommand
{
    public string CoalesceKey => $"direction:{connectionId}";

    protected override Task<bool> RunAsync(CommandContext context, CancellationToken cancellationToken)
        => Task.FromResult(context.GameStateService.ChangeDirectionByPlayerId(connectionId, direction));
}

public sealed class SteerCommand(string connectionId, double angle) : PlayerCommand<bool>, ICoalescableCommand
{
    public string CoalesceKey => $"steer:{connectionId}";

    protected override Task<bool> RunAsync(CommandContext context, CancellationToken cancellationToken)
        => Task.FromResult(context.GameStateService.SteerByPlayerId(connectionId, angle));
}

public sealed class SetBoostCommand(string connectionId, bool active) : PlayerCommand<bool>, ICoalescableCommand
{
    public string CoalesceKey => $"boost:{connectionId}";

    protected override Task<bool> RunAsync(CommandContext context, CancellationToken cancellationToken)
        => Task.FromResult(context.GameStateService.SetBoostByPlayerId(connectionId, active));
}

public sealed class ShootCommand(string connectionId) : PlayerCommand<bool>
{
    protected override async Task<bool> RunAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var player = FindByConnection(context.GameState, connectionId);
        if (player is not { IsAlive: true }) return false;
        await context.GameStateService.Shoot(context.GameState, context.GameState.Room, player);
        return true;
    }
}

/// <summary>Completes with the player that left, or null when the connection had no player.</summary>
public sealed class LeaveCommand(string connectionId) : PlayerCommand<IPlayer?>
{
    protected override async Task<IPlayer?> RunAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var player = FindByConnection(context.GameState, connectionId);
        if (player is null) return null;
        await context.GameStateService.DisconnectPlayer(context.GameState.Room, player);
        return player;
    }
}

/// <summary>
/// Completes with the created player, or null when the room refused. A signed-in player (<paramref name="accountId"/>) is tagged with
/// its account, so the stats of its life are counted.
/// </summary>
public sealed class JoinNewPlayerCommand(string connectionId, string nickname, Guid roomId, Guid? accountId = null) : PlayerCommand<IPlayer?>
{
    protected override async Task<IPlayer?> RunAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var player = await context.GameStateService.CreatePlayerInRoom(connectionId, nickname, roomId, cancellationToken);
        if (player is not null && accountId is Guid account)
            context.GameStateService.AttachAccount(player, account);
        return player;
    }
}

public sealed record ReconnectedPlayer(IPlayer Player, string OldConnectionId);

/// <summary>Completes with the reconnected player, or null when the player is unknown.</summary>
public sealed class ReconnectPlayerCommand(
    ITrackableId roomId, ITrackableId playerId, string newConnectionId) : PlayerCommand<ReconnectedPlayer?>
{
    protected override async Task<ReconnectedPlayer?> RunAsync(CommandContext context, CancellationToken cancellationToken)
    {
        var result = await context.GameStateService.ReconnectPlayer(roomId, playerId, newConnectionId);
        return result.HasValue ? new ReconnectedPlayer(result.Value.player, result.Value.oldPlayerConnectionId) : null;
    }
}
