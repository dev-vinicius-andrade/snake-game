using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Entities;

public record Player(Guid Id, string Name) : IPlayer;
