namespace Domain.Game.Rules;

/// <summary>Where a room is in a scheduled event: when the next is due (while none is going on), or when the one that is ends.</summary>
public sealed record EventWindow(DateTimeOffset? NextAt, DateTimeOffset? EndsAt)
{
    public static EventWindow None { get; } = new(null, null);

    public bool IsActive => EndsAt is not null;
}

public enum ScheduleAction
{
    None,
    /// <summary>The event begins now.</summary>
    Begin,
    /// <summary>The event is over.</summary>
    End
}

public sealed record ScheduleStep(EventWindow State, ScheduleAction Action);

/// <summary>
/// Times the events that come now and then and last a while (or are over at once): fruit shower, bounty, mine field, sugar rush.
/// Pure: the caller supplies the clock and acts on the result. Random numbers come from an injected function.
/// </summary>
public sealed class EventSchedule
{
    private readonly Func<double> _random;

    /// <param name="random">Gives numbers from 0 up to 1. Defaults to the shared generator.</param>
    public EventSchedule(Func<double>? random = null) => _random = random ?? Random.Shared.NextDouble;

    /// <summary>A number from 0 up to 1, for what the event itself draws (a spot, a snake).</summary>
    public double Next() => _random();

    /// <summary>
    /// Moves a room's event on. Call as often as it needs to be timed (every frame is fine). Whether the room is one the event is for
    /// is for the caller to decide; the event switched off, here or by the master switch, forgets everything.
    /// </summary>
    /// <param name="durationSeconds">How long it lasts once begun. 0: it is over at once and the next is scheduled.</param>
    public ScheduleStep Step(IScheduledEvent settings, bool masterEnabled, EventWindow state, DateTimeOffset now, int players, int durationSeconds)
    {
        if (!masterEnabled || !settings.Enabled) return new ScheduleStep(EventWindow.None, ScheduleAction.None);
        if (state.IsActive)
            return now >= state.EndsAt ? new ScheduleStep(Scheduled(settings, now), ScheduleAction.End) : new ScheduleStep(state, ScheduleAction.None);
        if (state.NextAt is not { } due) return new ScheduleStep(Scheduled(settings, now), ScheduleAction.None);
        if (now < due || players < settings.MinPlayers) return new ScheduleStep(state, ScheduleAction.None);
        return durationSeconds > 0
            ? new ScheduleStep(new EventWindow(null, now.AddSeconds(durationSeconds)), ScheduleAction.Begin)
            : new ScheduleStep(Scheduled(settings, now), ScheduleAction.Begin);
    }

    /// <summary>The state of a room whose next event is a random wait away.</summary>
    public EventWindow Scheduled(IScheduledEvent settings, DateTimeOffset now)
        => EventWindow.None with { NextAt = now.AddSeconds(settings.MinIntervalSeconds + (settings.MaxIntervalSeconds - settings.MinIntervalSeconds) * _random()) };
}
