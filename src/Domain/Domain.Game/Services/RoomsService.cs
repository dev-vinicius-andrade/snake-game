using System.Collections.Concurrent;
using Domain.Game.Entities;
using Library.Commons.Game.Domain.Constants;
using Library.Commons.Game.Domain.Entities;
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

    public IRoom CreateRoom(RoomOptions? options = null)
    {
        if (!IsAvailable())
            throw new MaxRoomReachedException(_serverConfiguration.Value.RoomsConfiguration.MaxRooms);
        var code = NormalizeCode(options?.Code);
        if (code is not null && GetRoomByCode(code) is not null)
            throw new UnableToCreateRoomException();
        var roomId = Guid.NewGuid();
        while (_rooms.ContainsKey(roomId))
            roomId = Guid.NewGuid();
        var room = new Room(roomId)
        {
            Width = _serverConfiguration.Value.RoomsConfiguration?.Width??RoomDefaultValues.Width,
            Height = _serverConfiguration.Value.RoomsConfiguration?.Height ?? RoomDefaultValues.Height,
            Name = options?.Name,
            Code = code,
            IsPrivate = options?.IsPrivate ?? false,
            MaxPlayers = LimitMaxPlayers(options?.MaxPlayers ?? 0),
            Gameplay = options?.Gameplay ?? new RoomGameplay(),
        };

        var added = _rooms.TryAdd(roomId, room);
        while (!added)
            added = _rooms.TryAdd(roomId, room);

        return room;
    }

    private static string? NormalizeCode(string? code) => string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

    private int LimitMaxPlayers(int requested) =>
        requested <= 0 ? 0 : Math.Min(requested, _serverConfiguration.Value.RoomsConfiguration.MaxPlayersPerRoom);

    public IRoom? TryGetRoom(Guid roomId) => _rooms.TryGetValue(roomId, out var room) ? room : null;

    public IRoom? GetRoomByCode(string code)
    {
        var normalized = NormalizeCode(code);
        return normalized is null ? null : _rooms.Values.FirstOrDefault(room => room.Code == normalized);
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
            room => !room.IsPrivate && !_playerRoomService.IsRoomFull(room)).ToList();

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