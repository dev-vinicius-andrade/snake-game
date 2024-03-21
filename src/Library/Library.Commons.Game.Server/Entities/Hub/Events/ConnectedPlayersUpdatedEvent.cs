using System.Collections.Generic;
using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Library.Commons.Game.Server.Entities.Hub.Events;

public record ConnectedPlayersUpdatedEvent
{

    public IList<IPlayer> Players { get; set; } = new List<IPlayer>();
}