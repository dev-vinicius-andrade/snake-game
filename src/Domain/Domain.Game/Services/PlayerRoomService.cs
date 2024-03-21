using System.Collections.Concurrent;
using Library.Commons.Game.Domain.Entities.Configurations;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace Domain.Game.Services;

public class PlayerRoomService: IPlayerRoomService
{
    private readonly IOptions<ServerConfiguration> _appSettings;
    private readonly ConcurrentDictionary<Guid, IList<IPlayer>> _roomsPlayers;
    private readonly IManagementApiService _managementApiService;
    private readonly ITrackableIdGeneratorService _trackableIdGeneratorService;

    public PlayerRoomService(IOptions<ServerConfiguration> appSettings, IManagementApiService managementApiService, ITrackableIdGeneratorService trackableIdGeneratorService)
    {
        _appSettings = appSettings;
        _managementApiService = managementApiService;
        _trackableIdGeneratorService = trackableIdGeneratorService;
        _roomsPlayers = new ConcurrentDictionary<Guid, IList<IPlayer>>();
    }

    public bool ConnectPlayer(IPlayer player, IRoom? room)
    {
        if (!_roomsPlayers.ContainsKey(room.Guid))
            _roomsPlayers.TryAdd(room.Guid, new List<IPlayer>());


        if (_roomsPlayers[room.Guid].Count >= _appSettings.Value.RoomsConfiguration.MaxPlayersPerRoom)
            return false;
        _roomsPlayers[room.Guid].Add(player);
        return true;
    }

    public bool DisconnectPlayer(IPlayer player, IRoom room)
    {
        if (!_roomsPlayers.TryGetValue(room.Guid, out var players) || !players.Contains(player))
            return false;
        players.Remove(player);
        return true;

    }

    public long CountPlayers(IRoom room)
    {
        return _roomsPlayers.TryGetValue(room.Guid, out var player) ? player.Count : 0;
    }

    public bool IsPlayerConnected(IPlayer player, IRoom room)
    {
        return _roomsPlayers.ContainsKey(room.Guid) && _roomsPlayers[room.Guid].Contains(player);
    }
    public bool IsRoomFull(IRoom room)
    {
        return _roomsPlayers.ContainsKey(room.Guid) && _roomsPlayers[room.Guid].Count >= _appSettings.Value.RoomsConfiguration.MaxPlayersPerRoom;
    }

    public IReadOnlyDictionary<Guid, IList<IPlayer>> GetRoomsPlayers()
    {
        return _roomsPlayers;
    }

    public ITrackableId? GetRoomTrackableId(IPlayer player)
    {
        foreach (var (room,players) in _roomsPlayers)
        {
            if (players.Any(x=>x.Guid.Equals(player.Guid)))
                return _trackableIdGeneratorService.Generate(room) ;
            
        }
        return null;
        
    }

    public void UpdatePlayer(IPlayer player, IRoom gameStateRoom)
    {
        if (!_roomsPlayers.TryGetValue(gameStateRoom.Guid, out var players)) return;
        var index = players.IndexOf(player);
        if (index != -1)
            players[index] = player;

    }
}