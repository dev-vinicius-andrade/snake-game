using Domain.Game.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;
using Library.Extensions.DependencyInjection.Attributes;
using System.Collections.Concurrent;
using Library.Commons.Game.Domain.Exceptions;

namespace Application.Game.Server.Services;

[AutoImport]
public class PlayerService:IPlayerService<Player>
{
    private readonly ConcurrentDictionary<Guid, Player> _players;

    public PlayerService(ConcurrentDictionary<Guid, Player>  players)
    {
        _players = players;
    }

    public Player Create(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Player name cannot be null or whitespace", nameof(name));
        var playerId = Guid.NewGuid();
        while(_players.ContainsKey(playerId))
            playerId = Guid.NewGuid();
        var player = new Player(playerId, name);
        if (_players.TryAdd(playerId, player))
            return player;
        throw new UnableToCreatePlayerException();
    }

    public Player Get(Guid id)
    {
        if (_players.TryGetValue(id, out var player))
            return player;

        throw new PlayerNotFoundException(id);
    }

    public IEnumerable<Player> GetAll()
    {
        return _players.Values;
    }
}