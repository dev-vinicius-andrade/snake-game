using Library.Commons.Game.Domain.Interfaces.Entities;

namespace Domain.Game.Rules;

/// <summary>A room can be named by its invite code, its name or its id, in any case.</summary>
public static class RoomNames
{
    public static bool Matches(string entry, IRoom room)
        => string.Equals(entry, room.Code, StringComparison.OrdinalIgnoreCase)
           || string.Equals(entry, room.Name, StringComparison.OrdinalIgnoreCase)
           || string.Equals(entry, room.Guid.ToString(), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the thing <paramref name="kind"/>:<paramref name="id"/> works in <paramref name="room"/>. What the room itself switched on or off
    /// (<see cref="Library.Commons.Game.Domain.Entities.RoomGameplay.Toggles"/>) decides; when the room made no choice, the old room lists do.
    /// </summary>
    public static bool Allows(string kind, string id, IReadOnlyCollection<string> rooms, IReadOnlyCollection<string> excluded, IRoom? room, bool offUntilNamed)
        => room is not null && room.Gameplay?.ToggleOf(kind, id) is { } chosen ? chosen : Allows(rooms, excluded, room, offUntilNamed);

    /// <summary>
    /// Whether something that is for the named <paramref name="rooms"/> (empty: every room, unless <paramref name="offUntilNamed"/>)
    /// and not for the <paramref name="excluded"/> ones works in <paramref name="room"/>.
    /// </summary>
    public static bool Allows(IReadOnlyCollection<string> rooms, IReadOnlyCollection<string> excluded, IRoom? room, bool offUntilNamed)
    {
        if (room is not null && excluded.Any(entry => Matches(entry, room))) return false;
        if (rooms.Count == 0) return !offUntilNamed;
        return room is not null && rooms.Any(entry => Matches(entry, room));
    }
}
