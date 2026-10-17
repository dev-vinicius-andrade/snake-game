using Domain.Game.Simulation;
using Library.Commons.Game.Domain.Constants;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Entities.Eventbus.Payloads;
using Library.Commons.Game.Domain.Exceptions;
using Library.Commons.Game.Domain.Interfaces.Services;

namespace Application.Game.Server.Services;

/// <summary>What to do with a message from the event bus once it was handled.</summary>
public enum MessageDisposition
{
    Acknowledge,

    /// <summary>Another game server may be able to handle it.</summary>
    Requeue,

    /// <summary>Nobody can handle it.</summary>
    Discard,
}

/// <summary>Puts a player who asked to join into a room on this server, or says that this server cannot.</summary>
public sealed class JoinRequestHandler(IRoomJoinRouter router, IManagementApiService management)
{
    public async Task<MessageDisposition> HandleAsync(PlayerJoinPayload payload)
    {
        var route = router.Resolve(payload.RoomId);
        switch (route.Outcome)
        {
            case JoinRouteOutcome.Joined:
                await management.PlayerJoinRoomAsync(payload.Id, payload.Nickname, route.Room!);
                return MessageDisposition.Acknowledge;
            case JoinRouteOutcome.Full:
                await management.PlayerJoinFailedAsync(payload.Id, JoinFailureReasons.RoomFull);
                return MessageDisposition.Discard;
            default:
                return MessageDisposition.Requeue;
        }
    }
}

/// <summary>Makes the private room a player asked for, when this server has room for one.</summary>
public sealed class RoomCreateRequestHandler(IRoomsService rooms, IManagementApiService management)
{
    public async Task<MessageDisposition> HandleAsync(RoomCreatePayload payload)
    {
        if (!rooms.IsAvailable()) return MessageDisposition.Requeue;
        try
        {
            var room = rooms.CreateRoom(new RoomOptions
            {
                IsPrivate = true,
                Name = payload.Name,
                Code = payload.Code,
                MaxPlayers = payload.MaxPlayers,
            });
            await management.RoomCreatedAsync(payload.RequesterId, room);
            return MessageDisposition.Acknowledge;
        }
        catch (UnableToCreateRoomException)
        {
            await management.RoomCreateFailedAsync(payload.RequesterId, JoinFailureReasons.CodeTaken);
            return MessageDisposition.Discard;
        }
    }
}

/// <summary>Tells everyone in a room that it is being closed.</summary>
public interface IRoomClosedNotifier
{
    Task NotifyClosedAsync(Guid roomId, CancellationToken cancellationToken = default);
}

/// <summary>Closes a room: players are told and removed through the room's own queue, then the room is forgotten.</summary>
public sealed class RoomCloser(
    IRoomsService rooms,
    IPlayerRoomService playerRooms,
    IGameStateService gameStates,
    IRoomInputQueue queue,
    IRoomClosedNotifier notifier,
    ITrackableIdGeneratorService ids)
{
    private static readonly TimeSpan DefaultLeaveTimeout = TimeSpan.FromSeconds(3);

    public async Task<bool> CloseAsync(Guid roomId, TimeSpan? leaveTimeout = null, CancellationToken cancellationToken = default)
    {
        if (rooms.TryGetRoom(roomId) is null) return false;

        await notifier.NotifyClosedAsync(roomId, cancellationToken);
        await RemovePlayersAsync(roomId, leaveTimeout ?? DefaultLeaveTimeout);
        var id = ids.Generate(roomId);
        playerRooms.RemoveRoom(roomId);
        gameStates.RemoveGameState(id);
        rooms.DeleteRoom(id);
        return true;
    }

    private async Task RemovePlayersAsync(Guid roomId, TimeSpan timeout)
    {
        var players = playerRooms.GetRoomsPlayers().TryGetValue(roomId, out var inRoom) ? inRoom.ToArray() : [];
        var leaving = players.Select(player => Leave(roomId, player.Id)).ToArray();
        await Task.WhenAny(Task.WhenAll(leaving), Task.Delay(timeout));
    }

    private Task Leave(Guid roomId, string connectionId)
    {
        var command = new LeaveCommand(connectionId);
        queue.Enqueue(roomId, command);
        return command.Completion;
    }
}
