using Library.Commons.Game.Domain.Interfaces.Services;

namespace Application.Game.Server.Services;

/// <summary>
/// Closes private rooms that stayed empty for too long, so abandoned invites do not pile up. A room nobody ever joined
/// counts from its creation, a room that emptied counts from the sweep that noticed it.
/// </summary>
public sealed class EmptyRoomReaper(
    IRoomsService rooms,
    IPlayerRoomService playerRooms,
    IRoomCloser closer,
    TimeProvider time,
    TimeSpan? emptyFor = null)
{
    public static readonly TimeSpan DefaultEmptyFor = TimeSpan.FromMinutes(10);

    private readonly TimeSpan _emptyFor = emptyFor ?? DefaultEmptyFor;
    private readonly Dictionary<Guid, DateTimeOffset> _emptySince = new();
    private readonly HashSet<Guid> _wasOccupied = new();

    public async Task SweepAsync(CancellationToken cancellationToken = default)
    {
        var now = time.GetUtcNow();
        var privateRooms = rooms.GetRooms().Where(room => room.IsPrivate).ToArray();
        Forget(privateRooms.Select(room => room.Guid).ToHashSet());
        foreach (var room in privateRooms)
        {
            if (!IsExpired(room.Guid, room.CreatedAt, playerRooms.CountPlayers(room), now)) continue;
            await CloseAsync(room.Guid, cancellationToken);
        }
    }

    private bool IsExpired(Guid roomId, DateTimeOffset createdAt, long players, DateTimeOffset now)
    {
        if (players > 0)
        {
            _emptySince.Remove(roomId);
            _wasOccupied.Add(roomId);
            return false;
        }
        if (!_emptySince.TryGetValue(roomId, out var since))
            _emptySince[roomId] = since = _wasOccupied.Contains(roomId) ? now : createdAt;
        return now - since >= _emptyFor;
    }

    private async Task CloseAsync(Guid roomId, CancellationToken cancellationToken)
    {
        try
        {
            await closer.CloseAsync(roomId, cancellationToken: cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Another sweep tries again. One stuck room must not keep the others open.
            return;
        }
        _emptySince.Remove(roomId);
        _wasOccupied.Remove(roomId);
    }

    private void Forget(HashSet<Guid> existing)
    {
        foreach (var gone in _emptySince.Keys.Where(id => !existing.Contains(id)).ToArray()) _emptySince.Remove(gone);
        _wasOccupied.RemoveWhere(id => !existing.Contains(id));
    }
}
