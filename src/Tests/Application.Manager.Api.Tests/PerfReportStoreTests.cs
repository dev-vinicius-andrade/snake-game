using System.Text.Json;
using Application.Manager.Api.Telemetry;
using Microsoft.Extensions.Time.Testing;

namespace Application.Manager.Api.Tests;

public sealed class PerfReportStoreTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));

    private static JsonElement Body(string json = "{}") => JsonDocument.Parse(json).RootElement.Clone();

    [Fact]
    public void Latest_GivesTheNewestReportsLast()
    {
        var store = new PerfReportStore(_time, capacity: 10);
        store.Add("a", Body("""{"n":1}"""));
        _time.Advance(TimeSpan.FromSeconds(5));
        store.Add("a", Body("""{"n":2}"""));

        var latest = store.Latest(session: null, take: 10);

        Assert.Equal(new[] { 1, 2 }, latest.Select(report => report.Data.GetProperty("n").GetInt32()));
        Assert.Equal(_time.GetUtcNow(), latest[^1].ReceivedAt);
    }

    [Fact]
    public void Add_ForgetsTheOldestWhenFull()
    {
        var store = new PerfReportStore(_time, capacity: 2);
        for (var n = 1; n <= 3; n++) store.Add("a", Body($$"""{"n":{{n}}}"""));

        var latest = store.Latest(session: null, take: 10);

        Assert.Equal(new[] { 2, 3 }, latest.Select(report => report.Data.GetProperty("n").GetInt32()));
    }

    [Fact]
    public void Latest_CanBeLimitedToOneSession()
    {
        var store = new PerfReportStore(_time, capacity: 10);
        store.Add("a", Body("""{"n":1}"""));
        store.Add("b", Body("""{"n":2}"""));
        store.Add("a", Body("""{"n":3}"""));

        var latest = store.Latest(session: "a", take: 10);

        Assert.Equal(new[] { 1, 3 }, latest.Select(report => report.Data.GetProperty("n").GetInt32()));
    }

    [Fact]
    public void Latest_TakesTheNewestWhenThereAreMoreThanAsked()
    {
        var store = new PerfReportStore(_time, capacity: 10);
        for (var n = 1; n <= 5; n++) store.Add("a", Body($$"""{"n":{{n}}}"""));

        var latest = store.Latest(session: null, take: 2);

        Assert.Equal(new[] { 4, 5 }, latest.Select(report => report.Data.GetProperty("n").GetInt32()));
    }
}
