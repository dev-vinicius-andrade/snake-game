using System.Net;
using System.Text;
using System.Text.Json;
using Application.Game.Server.Stats;
using Library.Commons.Game.Domain.Entities;
using NSubstitute;

namespace Application.Game.Server.Tests;

public sealed class ManagerAccountStatsClientTests
{
    private sealed class FakeManager(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return answer(request);
        }
    }

    private readonly Guid _account = Guid.Parse("6f1c2a52-6a8e-4f7e-9d3a-1b2c3d4e5f60");
    private readonly LifeStatsReport _report = new(
        Guid.Parse("0b9a1f8e-2c3d-4e5f-8a7b-9c0d1e2f3a4b"),
        new Dictionary<string, long> { [StatKeys.Games] = 1, [StatKeys.Kills] = 3 },
        new Dictionary<string, long> { [StatKeys.BestScore] = 120 });

    private static (ManagerAccountStatsClient Client, FakeManager Manager) With(Func<HttpRequestMessage, HttpResponseMessage> answer)
    {
        var manager = new FakeManager(answer);
        var clients = Substitute.For<IHttpClientFactory>();
        clients.CreateClient(ManagerAccountStatsClient.HttpClientName).Returns(_ => new HttpClient(manager) { BaseAddress = new Uri("http://manager:5000/") });
        return (new ManagerAccountStatsClient(clients), manager);
    }

    private static HttpResponseMessage Answer(HttpStatusCode status, string? json = null)
        => new(status) { Content = json is null ? null : new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task SendAsync_PostsTheReportToTheAccount_AsTheContractSays()
    {
        var (client, manager) = With(_ => Answer(HttpStatusCode.OK, """{ "unlocked": [] }"""));

        await client.SendAsync(_account, _report);

        Assert.Equal(HttpMethod.Post, manager.Request!.Method);
        Assert.Equal($"http://manager:5000/accounts/{_account}/stats", manager.Request.RequestUri!.ToString());
        using var body = JsonDocument.Parse(manager.Body!);
        Assert.Equal(_report.ReportId, body.RootElement.GetProperty("reportId").GetGuid());
        Assert.Equal(3, body.RootElement.GetProperty("counters").GetProperty("kills").GetInt64());
        Assert.Equal(120, body.RootElement.GetProperty("maxima").GetProperty("bestScore").GetInt64());
    }

    [Fact]
    public async Task SendAsync_Ok_IsAcceptedWithWhatItUnlocked()
    {
        var (client, _) = With(_ => Answer(HttpStatusCode.OK, """{ "unlocked": ["first-blood", "hunter"] }"""));

        var delivery = await client.SendAsync(_account, _report);

        Assert.Equal(StatsDeliveryOutcome.Accepted, delivery.Outcome);
        Assert.Equal(new[] { "first-blood", "hunter" }, delivery.Unlocked);
    }

    [Fact]
    public async Task SendAsync_OkWithABodyItCannotRead_IsAcceptedWithNothingUnlocked()
    {
        var (client, _) = With(_ => Answer(HttpStatusCode.OK, "not json"));

        var delivery = await client.SendAsync(_account, _report);

        Assert.Equal((StatsDeliveryOutcome.Accepted, 0), (delivery.Outcome, delivery.Unlocked.Count));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task SendAsync_AnAnswerThatWillNotChange_IsRefused(HttpStatusCode status)
    {
        var (client, _) = With(_ => Answer(status, """{ "error": "unknownAccount" }"""));

        Assert.Equal(StatsDeliveryOutcome.Refused, (await client.SendAsync(_account, _report)).Outcome);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.RequestTimeout)]
    public async Task SendAsync_AnAnswerWorthAnotherTry_IsFailed(HttpStatusCode status)
    {
        var (client, _) = With(_ => Answer(status));

        Assert.Equal(StatsDeliveryOutcome.Failed, (await client.SendAsync(_account, _report)).Outcome);
    }

    [Fact]
    public async Task SendAsync_TheManagerCannotBeReached_IsFailed()
    {
        var (client, _) = With(_ => throw new HttpRequestException("connection refused"));

        Assert.Equal(StatsDeliveryOutcome.Failed, (await client.SendAsync(_account, _report)).Outcome);
    }

    [Fact]
    public async Task SendAsync_ATimeout_IsFailed()
    {
        var (client, _) = With(_ => throw new TaskCanceledException("timeout"));

        Assert.Equal(StatsDeliveryOutcome.Failed, (await client.SendAsync(_account, _report)).Outcome);
    }
}
