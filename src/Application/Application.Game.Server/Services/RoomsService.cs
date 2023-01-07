using System.Collections.Concurrent;
using Application.Game.Server.Entities.Configurartions;
using Domain.Game.Entities;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Exceptions;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using Library.Extensions.DependencyInjection.Attributes;
using Microsoft.Extensions.Options;

namespace Application.Game.Server.Services;

[AutoImport]
public class RoomsService : IRoomsService<Room>
{
    private readonly ConcurrentDictionary<Guid, Room> _rooms;
    private readonly IPlayerRoomService<Room, Player> _playerRoomService;
    private readonly IOptions<AppSettings> _appSettings;

    public RoomsService(IOptions<AppSettings> appSettings, ConcurrentDictionary<Guid, Room> rooms, IPlayerRoomService<Room,Player> playerRoomService)
    {
        _rooms = rooms;
        _playerRoomService = playerRoomService;
        _appSettings = appSettings;
    }
    public bool IsAvailable()
    {
        return _rooms.Count() < _appSettings.Value.ServerConfiguration.RoomsConfiguration.MaxRooms;
    }

    public Room CreateRoom()
    {
        if (!IsAvailable())
            throw new MaxRoomReachedException(_appSettings.Value.ServerConfiguration.RoomsConfiguration.MaxRooms);
        var roomId = Guid.NewGuid();
        while (_rooms.ContainsKey(roomId))
            roomId = Guid.NewGuid();
        var room = new Room(roomId);
        var added = _rooms.TryAdd(roomId, room);
        while (!added)
            added = _rooms.TryAdd(roomId, room);

        return room;
    }

    public Room GetRoom(Guid id)
    {
        if (!_rooms.ContainsKey(id)) throw new RoomNotFoundException(id);

        if (!_rooms.TryGetValue(id, out var room)) return null;
        lock (room)
            return room;
    }

    public IEnumerable<Room> GetRooms()
    {
        return _rooms.Values;
    }

    public IEnumerable<Room> GetAvailableRooms()
    {

        return _rooms.Values.Where(
            room => !_playerRoomService.IsRoomFull(room)).ToList();

    }

    public Room DeleteRoom(Guid id)
    {
        if (!_rooms.TryRemove(id, out var removedRoom)) throw new RoomNotFoundException(id);
        return removedRoom;
    }
}