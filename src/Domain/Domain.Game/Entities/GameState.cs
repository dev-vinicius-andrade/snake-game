using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Library.Commons.Game.Domain.Entities;
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
    [JsonIgnore]
    public ConcurrentQueue<DamageDealt> Hits { get; } = new();
    [JsonIgnore]
    public ConcurrentDictionary<Guid, WorldStrike> Strikes { get; } = new();
    [JsonIgnore]
    public ConcurrentDictionary<Guid, WorldMine> Mines { get; } = new();
    [JsonIgnore]
    public WorldBounty? Bounty { get; set; }
    [JsonIgnore]
    public DateTimeOffset? FruitShowerEndsAt { get; set; }
    [JsonIgnore]
    public DateTimeOffset? SugarRushEndsAt { get; set; }
    [JsonIgnore]
    public WorldMaze? Maze { get; set; }
    [JsonIgnore]
    public WorldBoss? Boss { get; set; }
    [JsonIgnore]
    public WorldZone? Zone { get; set; }
}