using System.Runtime.Serialization;

namespace Library.Commons.Game.Domain.Interfaces.Entities;

public interface IId
{
    Guid Id { get; }
}