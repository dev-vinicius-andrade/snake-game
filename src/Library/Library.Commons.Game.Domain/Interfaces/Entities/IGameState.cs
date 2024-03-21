namespace Library.Commons.Game.Domain.Interfaces.Entities;

public interface IGameState
{
    IRoom Room { get; }
    IDictionary<Guid, IPlayer> Players { get; }

    IDictionary<string, IColor> Colors { get; set; }
    IDictionary<Guid, IFood> Foods { get; set; }
    IDictionary<Guid,IObstacle> Obstacles { get; set; }
}