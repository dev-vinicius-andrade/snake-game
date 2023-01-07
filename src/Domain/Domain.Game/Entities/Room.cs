using System.Runtime.Serialization;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Entities;
[Serializable]
public record Room(Guid Id) :IRoom;