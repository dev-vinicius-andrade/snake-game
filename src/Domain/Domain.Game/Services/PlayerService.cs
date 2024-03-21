using System.Collections.Concurrent;
using Domain.Game.Entities;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Exceptions;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;

namespace Domain.Game.Services;

public class PlayerService:IPlayerService
{
    private readonly ConcurrentDictionary<Guid, IPlayer> _players;

    public PlayerService(ConcurrentDictionary<Guid, IPlayer>  players)
    {
        _players = players;
    }

    public IPlayer Create(string id, string name, IColor backgroundColor, IMovementDirection direction,
        IPointAxis initialPosition, IList<IPointAxis>? paths = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Players name cannot be null or whitespace", nameof(name));
        var playerId = Guid.NewGuid();
        while(_players.ContainsKey(playerId))
            playerId = Guid.NewGuid();
        var player = new Player(playerId, name);
        player.Id = id;
        player.BackgroundColor = backgroundColor;
        player.CurrentPosition = initialPosition;
        player.Direction= direction;
        player.Path =  paths ?? new List<IPointAxis>();
        if (_players.TryAdd(playerId, player))
            return player;
        throw new UnableToCreatePlayerException();
    }

    public IPlayer Save(IPlayer player)
    {
        if(player.Guid == Guid.Empty)
            throw new ArgumentException("Players id cannot be empty", nameof(player.Guid));
        if(_players.ContainsKey(player.Guid))
            _players[player.Guid] = player;
        else if(!_players.TryAdd(player.Guid, player))
            throw new UnableToCreatePlayerException();
        return _players[player.Guid];
        
    }

    public IPlayer? Get(string contextConnectionId)
    {
        return _players.Values.FirstOrDefault(p => p.Id == contextConnectionId);
    }

    public IMovementDirection CreateDirection(Direction direction, IPointAxis axis)
    {
        return new MovementDirection(direction, axis);
    }

    public void Delete(ITrackableId player)
    {
        _players.TryRemove(player.Guid, out _);
    }

    public IPlayer Get(Guid id)
    {
        if (_players.TryGetValue(id, out var player))
            return player;

        throw new PlayerNotFoundException(id);
    }

    public IEnumerable<IPlayer> GetAll()
    {
        return _players.Values;
    }
}