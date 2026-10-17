using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;

namespace Domain.Game.Services;

public sealed class RoomJoinRouter(IRoomsService rooms, IPlayerRoomService playerRooms) : IRoomJoinRouter
{
    public JoinRoute Resolve(Guid? roomId) => roomId.HasValue ? ResolveGiven(roomId.Value) : ResolveAny();

    private JoinRoute ResolveGiven(Guid roomId)
    {
        var room = rooms.TryGetRoom(roomId);
        if (room is null) return new JoinRoute(JoinRouteOutcome.NotHere);
        return playerRooms.IsRoomFull(room) ? new JoinRoute(JoinRouteOutcome.Full) : new JoinRoute(JoinRouteOutcome.Joined, room);
    }

    private JoinRoute ResolveAny()
    {
        var open = rooms.GetAvailableRooms().FirstOrDefault();
        if (open is not null) return new JoinRoute(JoinRouteOutcome.Joined, open);
        return rooms.IsAvailable() ? new JoinRoute(JoinRouteOutcome.Joined, rooms.CreateRoom()) : new JoinRoute(JoinRouteOutcome.NoCapacity);
    }
}
