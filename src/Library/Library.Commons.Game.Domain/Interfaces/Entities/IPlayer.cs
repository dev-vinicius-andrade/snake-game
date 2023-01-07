using System.Runtime.Serialization;

namespace Library.Commons.Game.Domain.Interfaces.Entities;

public interface IPlayer:IId
{
    string Name { get; }
}