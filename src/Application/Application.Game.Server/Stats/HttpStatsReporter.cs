using System.Threading.Channels;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Interfaces.Services;

namespace Application.Game.Server.Stats;

/// <summary>
/// Sends the stats of ended lives to the manager (docs/ACHIEVEMENTS.md). <see cref="Report"/> only queues, so the game loop never waits;
/// <see cref="RunAsync"/> (StatsReportWorkerService) sends them one by one, tries a failed one again with a growing wait and the same
/// report id (the manager applies it once), drops it after <see cref="StatsReporterOptions.MaxAttempts"/>, and tells the player what
/// its account unlocked.
/// </summary>
public sealed class HttpStatsReporter : IStatsReporter
{
    private sealed record QueuedReport(Guid AccountId, LifeStatsReport Report);

    private readonly Channel<QueuedReport> _queue;
    private readonly IAccountStatsClient _client;
    private readonly IAchievementsNotifier _notifier;
    private readonly StatsReporterOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<HttpStatsReporter> _logger;

    public HttpStatsReporter(IAccountStatsClient client, IAchievementsNotifier notifier, StatsReporterOptions options, TimeProvider time,
        ILogger<HttpStatsReporter> logger)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(notifier);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(time);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.Capacity, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaxAttempts, 1);
        _client = client;
        _notifier = notifier;
        _options = options;
        _time = time;
        _logger = logger;
        _queue = Channel.CreateBounded<QueuedReport>(new BoundedChannelOptions(options.Capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });
    }

    public void Report(Guid accountId, LifeStatsReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (_queue.Writer.TryWrite(new QueuedReport(accountId, report)))
            return;
        _logger.LogWarning("Stats report {ReportId} of account {AccountId} dropped: {Capacity} reports are already waiting",
            report.ReportId, accountId, _options.Capacity);
    }

    /// <summary>Sends reports as they come, until stopped.</summary>
    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        while (await _queue.Reader.WaitToReadAsync(cancellationToken))
            await DrainAsync(cancellationToken);
    }

    /// <summary>Sends every report waiting now, one after the other.</summary>
    public async Task DrainAsync(CancellationToken cancellationToken = default)
    {
        while (_queue.Reader.TryRead(out var queued))
            await DeliverAsync(queued, cancellationToken);
    }

    private async Task DeliverAsync(QueuedReport queued, CancellationToken cancellationToken)
    {
        var delivery = await SendWithRetriesAsync(queued, cancellationToken);
        await ConcludeAsync(queued, delivery, cancellationToken);
    }

    private async Task<StatsDelivery> SendWithRetriesAsync(QueuedReport queued, CancellationToken cancellationToken)
    {
        var delivery = await SendOnceAsync(queued, cancellationToken);
        for (var attempt = 2; attempt <= _options.MaxAttempts && delivery.Outcome == StatsDeliveryOutcome.Failed; attempt++)
        {
            await Task.Delay(_options.DelayBefore(attempt), _time, cancellationToken);
            delivery = await SendOnceAsync(queued, cancellationToken);
        }
        return delivery;
    }

    private async Task<StatsDelivery> SendOnceAsync(QueuedReport queued, CancellationToken cancellationToken)
    {
        try
        {
            return await _client.SendAsync(queued.AccountId, queued.Report, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Stats report {ReportId} of account {AccountId} could not be sent", queued.Report.ReportId, queued.AccountId);
            return StatsDelivery.Failed;
        }
    }

    private Task ConcludeAsync(QueuedReport queued, StatsDelivery delivery, CancellationToken cancellationToken)
    {
        if (delivery.Outcome == StatsDeliveryOutcome.Accepted)
            return NotifyAsync(queued, delivery.Unlocked, cancellationToken);
        LogUndelivered(queued, delivery.Outcome);
        return Task.CompletedTask;
    }

    private void LogUndelivered(QueuedReport queued, StatsDeliveryOutcome outcome)
    {
        if (outcome == StatsDeliveryOutcome.Refused)
            _logger.LogWarning("Stats report {ReportId} of account {AccountId} was refused by the manager", queued.Report.ReportId, queued.AccountId);
        else
            _logger.LogError("Stats report {ReportId} of account {AccountId} dropped after {Attempts} attempts",
                queued.Report.ReportId, queued.AccountId, _options.MaxAttempts);
    }

    private async Task NotifyAsync(QueuedReport queued, IReadOnlyList<string> unlocked, CancellationToken cancellationToken)
    {
        if (unlocked.Count == 0)
            return;
        try
        {
            await _notifier.NotifyAsync(queued.AccountId, unlocked, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(exception, "Could not tell account {AccountId} what report {ReportId} unlocked", queued.AccountId, queued.Report.ReportId);
        }
    }
}
