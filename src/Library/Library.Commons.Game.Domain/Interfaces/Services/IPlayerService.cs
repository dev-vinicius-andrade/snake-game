using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Library.Commons.Game.Domain.Interfaces.Services;

public interface IPlayerService<out TPlayer>
where TPlayer:class,IPlayer
{
    TPlayer Create(string name);
    TPlayer Get(Guid id);
    IEnumerable<TPlayer> GetAll();

}