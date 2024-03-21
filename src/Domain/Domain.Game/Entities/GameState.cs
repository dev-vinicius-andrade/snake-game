using System.Collections.Concurrent;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Entities;

internal record GameState:IGameState
{
    public GameState(IRoom room)
    {
        Room = room;
        Players = new ConcurrentDictionary<Guid, IPlayer>();
        Colors = new ConcurrentDictionary<string, IColor>();
        Foods = new ConcurrentDictionary<Guid, IFood>();
        Obstacles = new ConcurrentDictionary<Guid, IObstacle>();
    }

    public IRoom Room { get; set; }
    public IDictionary<Guid,IPlayer> Players { get; set; }
    public IDictionary<string, IColor> Colors { get; set; }
    public IDictionary<Guid, IFood> Foods { get; set; }
    public IDictionary<Guid, IObstacle> Obstacles { get; set; }
}