namespace Application.Game.Server.Services;

/// <summary>
/// Sends to each connection one message at a time and keeps only the newest of those that pile up behind a slow one. A snapshot is the
/// whole state, not an event: an old one is worth nothing once a newer one exists, so a client on a poor link skips ahead instead of
/// queueing seconds of the past (which is what made its ping, and the room's game loop, wait). <see cref="Send"/> never waits.
/// </summary>
public sealed class LatestOnlySender<TMessage>
{
    private readonly Func<string, TMessage, Task> _deliver;
    private readonly Action<string, Exception> _onFailure;
    private readonly Dictionary<string, Lane> _lanes = new();

    public LatestOnlySender(Func<string, TMessage, Task> deliver, Action<string, Exception> onFailure)
    {
        _deliver = deliver ?? throw new ArgumentNullException(nameof(deliver));
        _onFailure = onFailure ?? throw new ArgumentNullException(nameof(onFailure));
    }

    private sealed class Lane
    {
        public TMessage? Waiting;
        public bool HasWaiting;
    }

    public void Send(string connectionId, TMessage message)
    {
        ArgumentException.ThrowIfNullOrEmpty(connectionId);
        ArgumentNullException.ThrowIfNull(message);
        lock (_lanes)
        {
            if (_lanes.TryGetValue(connectionId, out var lane))
            {
                lane.Waiting = message;
                lane.HasWaiting = true;
                return;
            }
            _lanes[connectionId] = new Lane();
        }
        _ = Task.Run(() => DrainAsync(connectionId, message));
    }

    private async Task DrainAsync(string connectionId, TMessage first)
    {
        var current = first;
        while (true)
        {
            await DeliverAsync(connectionId, current);
            if (!TryTakeWaiting(connectionId, out current)) return;
        }
    }

    private async Task DeliverAsync(string connectionId, TMessage message)
    {
        try
        {
            await _deliver(connectionId, message);
        }
        catch (Exception failure)
        {
            _onFailure(connectionId, failure);
        }
    }

    /// <summary>The newest message that waited; when there is none the lane is closed.</summary>
    private bool TryTakeWaiting(string connectionId, out TMessage message)
    {
        lock (_lanes)
        {
            message = default!;
            if (!_lanes.TryGetValue(connectionId, out var lane)) return false;
            if (!lane.HasWaiting)
            {
                _lanes.Remove(connectionId);
                return false;
            }
            message = lane.Waiting!;
            lane.Waiting = default;
            lane.HasWaiting = false;
            return true;
        }
    }
}
