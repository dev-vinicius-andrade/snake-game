using Domain.Game.Services;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;

namespace Application.Game.Server.Services;

public enum TriggerResult
{
    Started,
    /// <summary>It cannot start in that room just now: it is off there, nobody is in the room, or it is already going.</summary>
    NotNow,
    RoomNotFound,
    UnknownEvent
}

/// <summary>Starts a world event in a room by its name.</summary>
public interface IEventTriggers
{
    TriggerResult Trigger(IGameState gameState, string kind);
}

/// <summary>The events an administrator can start by name: lightning, meteors, turret, zone, fruitshower, bounty, mines, sugarrush.</summary>
public sealed class EventTriggers(
    WorldEventRunner events, TurretRunner turrets, ZoneRunner zones,
    FruitShowerRunner fruitShowers, BountyRunner bounties, MineFieldRunner mines, SugarRushRunner sugarRushes) : IEventTriggers
{
    public static IReadOnlyList<string> Names { get; } = ["lightning", "meteors", "turret", "zone", "fruitshower", "bounty", "mines", "sugarrush"];

    public TriggerResult Trigger(IGameState gameState, string kind)
    {
        var started = kind.ToLowerInvariant() switch
        {
            "lightning" => events.Trigger(gameState, Domain.Game.Rules.WorldEventKind.Lightning),
            "meteors" => events.Trigger(gameState, Domain.Game.Rules.WorldEventKind.Meteor),
            "turret" => turrets.Trigger(gameState),
            "zone" => zones.Trigger(gameState),
            "fruitshower" => fruitShowers.Trigger(gameState),
            "bounty" => bounties.Trigger(gameState),
            "mines" => mines.Trigger(gameState),
            "sugarrush" => sugarRushes.Trigger(gameState),
            _ => (bool?)null
        };
        return started is null ? TriggerResult.UnknownEvent : started.Value ? TriggerResult.Started : TriggerResult.NotNow;
    }
}

/// <summary>What the manager can ask of a room of this server: change its gameplay, start an event in it.</summary>
public sealed class RoomGameplayService(IRoomsService rooms, IGameStateService gameStates, ITrackableIdGeneratorService ids, IEventTriggers triggers)
{
    /// <summary>Gives the room these gameplay choices. False when the room is not here.</summary>
    public bool Set(Guid roomId, RoomGameplay gameplay)
    {
        ArgumentNullException.ThrowIfNull(gameplay);
        if (rooms.TryGetRoom(roomId) is not { } room) return false;
        room.Gameplay = gameplay;
        return true;
    }

    public TriggerResult Trigger(Guid roomId, string kind)
    {
        if (rooms.TryGetRoom(roomId) is null) return TriggerResult.RoomNotFound;
        if (!EventTriggers.Names.Contains(kind.ToLowerInvariant())) return TriggerResult.UnknownEvent;
        var state = gameStates.GetGameState(ids.Generate(roomId));
        return state is null ? TriggerResult.RoomNotFound : triggers.Trigger(state, kind);
    }
}
