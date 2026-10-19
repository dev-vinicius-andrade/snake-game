using System.Net;
using System.Text.Json;
using Library.Commons.Game.Domain.Entities;

namespace Application.Game.Server.Stats;

/// <summary>The body of <c>POST /accounts/{id}/stats</c> (docs/ACHIEVEMENTS.md).</summary>
public sealed class StatsReportBody
{
    public Guid ReportId { get; set; }
    public Dictionary<string, long> Counters { get; set; } = [];
    public Dictionary<string, long> Maxima { get; set; } = [];

    public static implicit operator StatsReportBody(LifeStatsReport report) => new()
    {
        ReportId = report.ReportId,
        Counters = new Dictionary<string, long>(report.Counters),
        Maxima = new Dictionary<string, long>(report.Maxima),
    };
}

/// <summary>The answer of <c>POST /accounts/{id}/stats</c>.</summary>
public sealed class StatsAnswerBody
{
    public List<string>? Unlocked { get; set; }
}

/// <summary>
/// Posts reports to the manager with the named <see cref="HttpClientName"/> client: the manager's address and the api key the game server
/// already uses toward it. 2xx: accepted, with what it unlocked. 404 and other 4xx (not 408 or 429): refused for good. The rest: failed.
/// </summary>
public sealed class ManagerAccountStatsClient(IHttpClientFactory clients) : IAccountStatsClient
{
    public const string HttpClientName = "manager-account-stats";

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    public async Task<StatsDelivery> SendAsync(Guid accountId, LifeStatsReport report, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        try
        {
            using var response = await clients.CreateClient(HttpClientName)
                .PostAsJsonAsync($"accounts/{accountId}/stats", (StatsReportBody)report, WebJson, cancellationToken);
            return await ReadAsync(response, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return StatsDelivery.Failed;
        }
    }

    private static async Task<StatsDelivery> ReadAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
            return StatsDelivery.Accepted(await UnlockedAsync(response, cancellationToken));
        return IsRefusedForGood(response.StatusCode) ? StatsDelivery.Refused : StatsDelivery.Failed;
    }

    private static async Task<IReadOnlyList<string>> UnlockedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var answer = await response.Content.ReadFromJsonAsync<StatsAnswerBody>(WebJson, cancellationToken);
            return answer?.Unlocked?.Where(id => !string.IsNullOrWhiteSpace(id)).ToList() ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool IsRefusedForGood(HttpStatusCode status)
        => (int)status is >= 400 and < 500 && status is not (HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests);
}
