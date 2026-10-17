using Application.Game.Server.Hubs;
using Library.Commons.Game.Server.Entities.Hub.Events;
using Library.Commons.Game.Server.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace Application.Game.Server.Services;

public sealed class HubRoomClosedNotifier(IHubContext<GameHub, IGameHubSendEvents> hub) : IRoomClosedNotifier
{
    public Task NotifyClosedAsync(Guid roomId, CancellationToken cancellationToken = default)
        => hub.Clients.Group(roomId.ToString()).RoomClosed(new RoomClosedEvent());
}
