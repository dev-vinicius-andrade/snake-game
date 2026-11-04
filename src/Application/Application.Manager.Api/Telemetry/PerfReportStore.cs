using System.Text.Json;

namespace Application.Manager.Api.Telemetry;

/// <summary>What a page told about how smooth the game was for it: who (a random id of the page, no one's name), when, and the numbers as it sent them.</summary>
public sealed record PerfReport(string Session, DateTimeOffset ReceivedAt, JsonElement Data);

/// <summary>
/// The last reports pages sent from their frame meter (<c>?perf</c>), kept in memory so an administrator can read them. When it is full the
/// oldest is forgotten: this is for looking at what a phone is doing right now, not for keeping a history.
/// </summary>
public sealed class PerfReportStore(TimeProvider time, int capacity = 500)
{
    private readonly Queue<PerfReport> _reports = new();

    public void Add(string session, JsonElement data)
    {
        lock (_reports)
        {
            _reports.Enqueue(new PerfReport(session, time.GetUtcNow(), data));
            while (_reports.Count > capacity) _reports.Dequeue();
        }
    }

    /// <summary>The newest <paramref name="take"/> reports, oldest first; only those of one session when it is given.</summary>
    public IReadOnlyList<PerfReport> Latest(string? session, int take)
    {
        lock (_reports)
        {
            var matching = _reports.Where(report => session is null || report.Session == session);
            return matching.TakeLast(Math.Max(take, 0)).ToList();
        }
    }
}
