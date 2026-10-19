using Application.Game.Server.Stats;
using Library.Commons.Game.Domain.Entities;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Application.Game.Server.Tests;

public sealed class HttpStatsReporterTests
{
    private readonly IAccountStatsClient _client = Substitute.For<IAccountStatsClient>();
    private readonly IAchievementsNotifier _notifier = Substitute.For<IAchievementsNotifier>();
    private readonly ILogger<HttpStatsReporter> _logger = Substitute.For<ILogger<HttpStatsReporter>>();
    private readonly List<(Guid AccountId, Guid ReportId)> _sent = [];
    private readonly Guid _account = Guid.NewGuid();

    private static readonly StatsReporterOptions NoWaiting = new() { FirstRetryDelay = TimeSpan.Zero, MaxRetryDelay = TimeSpan.Zero, MaxAttempts = 3 };

    private HttpStatsReporter Reporter(StatsReporterOptions? options = null)
        => new(_client, _notifier, options ?? NoWaiting, TimeProvider.System, _logger);

    private static LifeStatsReport NewReport()
        => new(Guid.NewGuid(), new Dictionary<string, long> { [StatKeys.Kills] = 1 }, new Dictionary<string, long> { [StatKeys.BestScore] = 10 });

    private void ManagerAnswers(params StatsDelivery[] deliveries)
    {
        var answers = new Queue<StatsDelivery>(deliveries);
        _client.SendAsync(Arg.Any<Guid>(), Arg.Any<LifeStatsReport>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            _sent.Add((call.ArgAt<Guid>(0), call.ArgAt<LifeStatsReport>(1).ReportId));
            return answers.Count > 1 ? answers.Dequeue() : answers.Peek();
        });
    }

    private int LogsAt(LogLevel level)
        => _logger.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(ILogger.Log) && (LogLevel)call.GetArguments()[0]! == level);

    [Fact]
    public async Task Drain_AnAcceptedReport_TellsThePlayerWhatItUnlocked()
    {
        ManagerAnswers(StatsDelivery.Accepted(["first-blood"]));
        var reporter = Reporter();
        var report = NewReport();

        reporter.Report(_account, report);
        await reporter.DrainAsync();

        Assert.Equal(new[] { (_account, report.ReportId) }, _sent);
        await _notifier.Received(1).NotifyAsync(_account, Arg.Is<IReadOnlyList<string>>(ids => ids.SequenceEqual(new[] { "first-blood" })), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Drain_NothingUnlocked_TellsNobody()
    {
        ManagerAnswers(StatsDelivery.Accepted([]));
        var reporter = Reporter();

        reporter.Report(_account, NewReport());
        await reporter.DrainAsync();

        await _notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default, default!, default);
    }

    [Fact]
    public async Task Drain_AFailedSending_IsTriedAgainWithTheSameReportId()
    {
        ManagerAnswers(StatsDelivery.Failed, StatsDelivery.Failed, StatsDelivery.Accepted(["hunter"]));
        var reporter = Reporter();
        var report = NewReport();

        reporter.Report(_account, report);
        await reporter.DrainAsync();

        Assert.Equal(3, _sent.Count);
        Assert.All(_sent, sent => Assert.Equal((_account, report.ReportId), sent));
        await _notifier.Received(1).NotifyAsync(_account, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Drain_AReportThatKeepsFailing_IsDroppedAfterTheLastAttempt_AndTheNextOneStillGoes()
    {
        ManagerAnswers(StatsDelivery.Failed);
        var reporter = Reporter();
        var failing = NewReport();
        var next = NewReport();

        reporter.Report(_account, failing);
        reporter.Report(_account, next);
        await reporter.DrainAsync();

        Assert.Equal(NoWaiting.MaxAttempts, _sent.Count(sent => sent.ReportId == failing.ReportId));
        Assert.Contains(_sent, sent => sent.ReportId == next.ReportId);
        Assert.Equal(2, LogsAt(LogLevel.Error));
        await _notifier.DidNotReceiveWithAnyArgs().NotifyAsync(default, default!, default);
    }

    [Fact]
    public async Task Drain_ARefusedReport_IsNotTriedAgain()
    {
        ManagerAnswers(StatsDelivery.Refused);
        var reporter = Reporter();

        reporter.Report(_account, NewReport());
        await reporter.DrainAsync();

        Assert.Single(_sent);
        Assert.Equal(1, LogsAt(LogLevel.Warning));
    }

    [Fact]
    public async Task Drain_AClientThatThrows_CountsAsAFailedSending()
    {
        _client.SendAsync(Arg.Any<Guid>(), Arg.Any<LifeStatsReport>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<StatsDelivery>(new InvalidOperationException("boom")), Task.FromResult(StatsDelivery.Accepted(["hunter"])));
        var reporter = Reporter();

        reporter.Report(_account, NewReport());
        await reporter.DrainAsync();

        await _client.Received(2).SendAsync(_account, Arg.Any<LifeStatsReport>(), Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyAsync(_account, Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Drain_ANotifierThatThrows_DoesNotStopTheNextReports()
    {
        ManagerAnswers(StatsDelivery.Accepted(["hunter"]));
        _notifier.NotifyAsync(default, default!, default).ReturnsForAnyArgs(Task.FromException(new InvalidOperationException("boom")));
        var reporter = Reporter();

        reporter.Report(_account, NewReport());
        reporter.Report(_account, NewReport());
        await reporter.DrainAsync();

        Assert.Equal(2, _sent.Count);
    }

    [Fact]
    public async Task Report_WhenTheQueueIsFull_DropsTheNewReportAtOnce()
    {
        ManagerAnswers(StatsDelivery.Accepted([]));
        var reporter = Reporter(NoWaiting with { Capacity = 2 });
        var kept = new[] { NewReport(), NewReport() };
        var dropped = NewReport();

        foreach (var report in kept)
            reporter.Report(_account, report);
        reporter.Report(_account, dropped);
        await reporter.DrainAsync();

        Assert.Equal(kept.Select(report => report.ReportId), _sent.Select(sent => sent.ReportId));
        Assert.Equal(1, LogsAt(LogLevel.Warning));
    }

    [Fact]
    public async Task RunAsync_SendsReportsAsTheyCome_UntilStopped()
    {
        ManagerAnswers(StatsDelivery.Accepted(["first-steps"]));
        var reporter = Reporter();
        using var stop = new CancellationTokenSource();
        var notified = new TaskCompletionSource();
        _notifier.When(notifier => notifier.NotifyAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()))
            .Do(_ => notified.TrySetResult());
        var running = reporter.RunAsync(stop.Token);

        reporter.Report(_account, NewReport());
        await notified.Task.WaitAsync(TimeSpan.FromSeconds(5));
        stop.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => running);
    }
}

public sealed class StatsReporterOptionsTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 1)]
    [InlineData(3, 2)]
    [InlineData(4, 4)]
    [InlineData(5, 8)]
    [InlineData(10, 30)]
    public void DelayBefore_DoublesFromTheFirstRetry_UpToTheCap(int attempt, double seconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(seconds), new StatsReporterOptions().DelayBefore(attempt));
    }
}
