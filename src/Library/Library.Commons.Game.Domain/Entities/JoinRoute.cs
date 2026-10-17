using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Library.Commons.Game.Domain.Entities;

public enum JoinRouteOutcome
{
    /// <summary>The player can join <see cref="JoinRoute.Room"/> on this server.</summary>
    Joined,

    /// <summary>The wanted room is not on this server. Another server may have it.</summary>
    NotHere,

    /// <summary>The wanted room is here but full.</summary>
    Full,

    /// <summary>No open room and this server cannot make another.</summary>
    NoCapacity,
}

public sealed record JoinRoute(JoinRouteOutcome Outcome, IRoom? Room = null);
