using System.Collections.Concurrent;
using Application.Game.Server.Entities.Configurartions;
using Domain.Game.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using Library.Extensions.DependencyInjection.Attributes;
using Microsoft.Extensions.Options;

namespace Application.Game.Server.Services;

[AutoImport(ServiceLifetime.Singleton)]
public class PlayerRoomService: IPlayerRoomService<Room,Player>
{
    private readonly IOptions<AppSettings> _appSettings;
    private readonly ConcurrentDictionary<Guid, IList<Player>> _roomsPlayers;

    public PlayerRoomService(IOptions<AppSettings> appSettings)
    {
        _appSettings = appSettings;
        _roomsPlayers = new ConcurrentDictionary<Guid, IList<Player>>();
    }

    public bool ConnectPlayer(Player player, Room room)
    {
        if (!_roomsPlayers.ContainsKey(room.Id))
            _roomsPlayers.TryAdd(room.Id, new List<Player>());


        if (_roomsPlayers[room.Id].Count >= _appSettings.Value.ServerConfiguration.RoomsConfiguration.MaxPlayersPerRoom)
            return false;
        _roomsPlayers[room.Id].Add(player);
        return true;
    }

    public bool DisconnectPlayer(Player player, Room room)
    {
        return _roomsPlayers.ContainsKey(room.Id) && _roomsPlayers[room.Id].Remove(player);
    }

    public long CountPlayers(Room room)
    {
        return _roomsPlayers.ContainsKey(room.Id) ? _roomsPlayers[room.Id].Count : 0;
    }

    public bool IsPlayerConnected(Player player, Room room)
    {
        return _roomsPlayers.ContainsKey(room.Id) && _roomsPlayers[room.Id].Contains(player);
    }
    public bool IsRoomFull(Room room)
    {
        return _roomsPlayers.ContainsKey(room.Id) && _roomsPlayers[room.Id].Count >= _appSettings.Value.ServerConfiguration.RoomsConfiguration.MaxPlayersPerRoom;
    }
}