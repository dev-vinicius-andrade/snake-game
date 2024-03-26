using System.Collections.Concurrent;
using Domain.Game.Entities;
using Library.Commons.Game.Domain.Constants;
using Library.Commons.Game.Domain.Entities.Configurations;
using Library.Commons.Game.Domain.Exceptions;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace Domain.Game.Services;


public class RoomsService : IRoomsService
{
    private readonly ConcurrentDictionary<Guid, IRoom> _rooms;
    private readonly IPlayerRoomService _playerRoomService;
    private readonly IOptions<ServerConfiguration> _serverConfiguration;

    public RoomsService(IOptions<ServerConfiguration> serverConfiguration, ConcurrentDictionary<Guid, IRoom> rooms, IPlayerRoomService playerRoomService)
    {
        _rooms = rooms;
        _playerRoomService = playerRoomService;
        _serverConfiguration = serverConfiguration;
    }
    public bool IsAvailable()
    {
        return _rooms.Count() < _serverConfiguration.Value.RoomsConfiguration.MaxRooms;
    }

    public IRoom CreateRoom()
    {
        if (!IsAvailable())
            throw new MaxRoomReachedException(_serverConfiguration.Value.RoomsConfiguration.MaxRooms);
        var roomId = Guid.NewGuid();
        while (_rooms.ContainsKey(roomId))
            roomId = Guid.NewGuid();
        var room = new Room(roomId)
        {
            Width = _serverConfiguration.Value.RoomsConfiguration?.Width??RoomDefaultValues.Width,
            Height = _serverConfiguration.Value.RoomsConfiguration?.Height ?? RoomDefaultValues.Height,
        };

        var added = _rooms.TryAdd(roomId, room);
        while (!added)
            added = _rooms.TryAdd(roomId, room);

        return room;
    }

    public IRoom? GetRoom(ITrackableId id)
    {
        if (!_rooms.ContainsKey(id.Guid)) throw new RoomNotFoundException(id.Guid);

        if (!_rooms.TryGetValue(id.Guid, out var room)) return null;
        lock (room)
            return room;
    }

    public IEnumerable<IRoom> GetRooms()
    {
        return _rooms.Values;
    }

    public IEnumerable<IRoom> GetAvailableRooms()
    {

        return _rooms.Values.Where(
            room => !_playerRoomService.IsRoomFull(room)).ToList();

    }

    public IRoom DeleteRoom(ITrackableId id)
    {
        if (!_rooms.TryRemove(id.Guid, out var removedRoom)) throw new RoomNotFoundException(id.Guid);
        return removedRoom;
    }

    public bool CanCreate()
    {
        return _rooms.Count() + 1 < _serverConfiguration.Value.RoomsConfiguration.MaxRooms;
    }
}