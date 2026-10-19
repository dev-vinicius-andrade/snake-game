using System.Collections.Concurrent;
using Library.Commons.Api.Entities.Requests.Room;

namespace Application.Manager.Api.Rooms;

/// <summary>A room as the manager knows it, and where it lives.</summary>
/// <param name="PendingSince">Set for a room that a game server announced but whose heartbeat has not listed it yet.</param>
public sealed record RegisteredRoom(RoomSummary Summary, string GameServerUrl, string BaseUrl, DateTimeOffset LastSeen, DateTimeOffset? PendingSince = null);

/// <summary>
/// Which rooms exist on which game server. Game servers announce new private rooms and send all of their rooms
/// regularly; rooms of a server that stops sending are forgotten.
/// </summary>
public sealed class RoomRegistry(TimeProvider time)
{
    /// <summary>How long a just announced room survives a heartbeat that was built before the room existed.</summary>
    public static readonly TimeSpan NewRoomGrace = TimeSpan.FromSeconds(10);

    private readonly ConcurrentDictionary<Guid, RegisteredRoom> _rooms = new();

    public void Register(RoomCreatedRequest created)
    {
        var now = time.GetUtcNow();
        var summary = new RoomSummary
        {
            Id = created.RoomId,
            Code = created.Code,
            Name = created.Name,
            IsPrivate = true,
            Players = 0,
            MaxPlayers = created.MaxPlayers,
            CreatedAt = now,
            Gameplay = created.Gameplay,
        };
        _rooms[created.RoomId] = new RegisteredRoom(summary, created.GameServerUrl, created.BaseUrl, now, now);
    }

    public void Update(RoomHeartbeatRequest heartbeat)
    {
        var now = time.GetUtcNow();
        var reported = heartbeat.Rooms.Select(room => room.Id).ToHashSet();
        RemoveMissing(heartbeat.BaseUrl, reported, now);
        foreach (var summary in heartbeat.Rooms)
            _rooms[summary.Id] = new RegisteredRoom(summary, heartbeat.GameServerUrl, heartbeat.BaseUrl, now);
    }

    private void RemoveMissing(string baseUrl, HashSet<Guid> reported, DateTimeOffset now)
    {
        var missing = _rooms.Values.Where(room => room.BaseUrl == baseUrl && !reported.Contains(room.Summary.Id) && !IsInGrace(room, now));
        foreach (var room in missing.ToArray())
            _rooms.TryRemove(room.Summary.Id, out _);
    }

    private static bool IsInGrace(RegisteredRoom room, DateTimeOffset now) => room.PendingSince is { } since && now - since <= NewRoomGrace;

    public RegisteredRoom? Get(Guid id) => _rooms.GetValueOrDefault(id);

    public RegisteredRoom? FindByCode(string code)
        => _rooms.Values.FirstOrDefault(room => string.Equals(room.Summary.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase));

    public bool IsCodeTaken(string code) => FindByCode(code) is not null;

    public IReadOnlyList<RegisteredRoom> List() => _rooms.Values.OrderBy(room => room.Summary.CreatedAt).ToArray();

    /// <summary>Remembers what a room now does with the gameplay, until its server says it again.</summary>
    public bool SetGameplay(Guid id, Dictionary<string, string> gameplay)
    {
        if (!_rooms.TryGetValue(id, out var room)) return false;
        room.Summary.Gameplay = gameplay;
        return true;
    }

    public bool Remove(Guid id) => _rooms.TryRemove(id, out _);

    /// <summary>Forgets the rooms of servers that were not heard from for <paramref name="serverTtl"/>. Returns how many.</summary>
    public int Prune(TimeSpan serverTtl)
    {
        var limit = time.GetUtcNow() - serverTtl;
        var stale = _rooms.Values.Where(room => room.LastSeen < limit).ToArray();
        return stale.Count(room => _rooms.TryRemove(room.Summary.Id, out _));
    }
}
