using System.Text.Json;
using Library.Commons.Game.Domain.Entities;

namespace Application.Manager.Api.Accounts;

/// <summary>
/// Stats and unlocks in memory, and in a JSON file when a path is given (no path: memory only, for tests and dev). Thread safe: a report is
/// checked, added, evaluated and saved under one lock, so reports sent at once add up exactly and an achievement unlocks only once.
/// Written like <see cref="FileAccountStore"/>: every change goes to a temp file in the same folder, flushed and moved over the file, and when
/// that fails the exception is thrown and nothing changes in memory. A file that cannot be read as stats is renamed to
/// <c>&lt;path&gt;.corrupt-&lt;utc time&gt;</c> and the store starts empty; a file that cannot be read at all makes the constructor throw.
/// Every change rewrites the whole file: fine for the few thousand accounts this is meant for, Postgres is the plan beyond that.
/// </summary>
public sealed class FileStatsStore : IStatsStore
{
    private const int FileVersion = 1;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private readonly string? _path;
    private readonly ILogger? _logger;
    private readonly object _lock = new();
    private IReadOnlyDictionary<Guid, AccountStats> _byAccount = new Dictionary<Guid, AccountStats>();

    public FileStatsStore(string? path, ILogger? logger = null)
    {
        _path = string.IsNullOrWhiteSpace(path) ? null : path;
        _logger = logger;
        lock (_lock) _byAccount = Load();
    }

    /// <summary>Called with the temp file once it is written and flushed, right before it replaces the file. Tests use it to cut a write short.</summary>
    internal Action<string>? BeforeReplacingFile { get; set; }

    /// <summary>The stats of every account that has any, as a copy. For moving them somewhere else (the database import).</summary>
    internal IReadOnlyDictionary<Guid, AccountStats> All()
    {
        lock (_lock) return new Dictionary<Guid, AccountStats>(_byAccount);
    }

    public AccountStats Get(Guid accountId)
    {
        lock (_lock) return _byAccount.GetValueOrDefault(accountId) ?? AccountStats.Empty;
    }

    public StatsApplyResult Apply(Guid accountId, Guid reportId, IReadOnlyDictionary<string, long>? counters, IReadOnlyDictionary<string, long>? maxima,
        DateTimeOffset now, IReadOnlyList<AchievementDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var report = StatsReport.TryCreate(reportId, counters, maxima);
        if (report is null)
            return StatsApplyResult.Rejected;
        lock (_lock) return ApplyReport(accountId, report, now, definitions);
    }

    private StatsApplyResult ApplyReport(Guid accountId, StatsReport report, DateTimeOffset now, IReadOnlyList<AchievementDefinition> definitions)
    {
        var current = _byAccount.GetValueOrDefault(accountId) ?? AccountStats.Empty;
        if (current.HasApplied(report.Id))
            return StatsApplyResult.AlreadyApplied;
        var change = current.With(report, now, definitions);
        var updated = new Dictionary<Guid, AccountStats>(_byAccount) { [accountId] = change.Stats };
        Save(updated);
        _byAccount = updated;
        return StatsApplyResult.Applied(change.Unlocked);
    }

    private IReadOnlyDictionary<Guid, AccountStats> Load()
    {
        if (_path is null || !File.Exists(_path)) return new Dictionary<Guid, AccountStats>();
        var text = File.ReadAllText(_path);
        try
        {
            return Parse(text);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            Quarantine(_path, exception);
            return new Dictionary<Guid, AccountStats>();
        }
    }

    private static IReadOnlyDictionary<Guid, AccountStats> Parse(string text)
    {
        var document = JsonSerializer.Deserialize<StoredDocument>(text, Json);
        if (document?.Accounts is null) throw new InvalidDataException("The file holds no stats list.");
        var byAccount = new Dictionary<Guid, AccountStats>();
        foreach (var stored in document.Accounts)
            AddLoaded(byAccount, stored ?? throw new InvalidDataException("An account's stats are empty."));
        return byAccount;
    }

    private static void AddLoaded(Dictionary<Guid, AccountStats> byAccount, StoredAccountStats stored)
    {
        if (stored.AccountId == Guid.Empty) throw new InvalidDataException("Stats without an account id.");
        if (!byAccount.TryAdd(stored.AccountId, stored.ToStats())) throw new InvalidDataException($"The stats of account {stored.AccountId} appear twice.");
    }

    private void Quarantine(string path, Exception reason)
    {
        var target = $"{path}.corrupt-{DateTimeOffset.UtcNow:yyyyMMdd'T'HHmmssfff'Z'}";
        if (File.Exists(target)) target += "-" + Guid.NewGuid().ToString("N")[..8];
        File.Move(path, target);
        _logger?.LogError(reason, "The stats file {Path} could not be read; it was kept as {QuarantinePath} and the store starts empty", path, target);
    }

    private void Save(IReadOnlyDictionary<Guid, AccountStats> byAccount)
    {
        if (_path is null) return;
        var temp = $"{_path}.{Guid.NewGuid():N}.tmp";
        try
        {
            WriteAndFlush(temp, new StoredDocument(FileVersion, byAccount.Select(StoredAccountStats.From).ToList<StoredAccountStats?>()));
            BeforeReplacingFile?.Invoke(temp);
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception exception)
        {
            DeleteQuietly(temp);
            _logger?.LogError(exception, "The stats could not be saved to {Path}; the report was not applied", _path);
            throw;
        }
    }

    private static void WriteAndFlush(string temp, StoredDocument document)
    {
        var folder = Path.GetDirectoryName(Path.GetFullPath(temp));
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        using var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, document, Json);
        stream.Flush(flushToDisk: true);
    }

    private static void DeleteQuietly(string temp)
    {
        try
        {
            File.Delete(temp);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record StoredDocument(int Version, List<StoredAccountStats?>? Accounts);

    private sealed record StoredAccountStats(
        Guid AccountId,
        Dictionary<string, long>? Counters,
        Dictionary<string, long>? Maxima,
        List<Guid>? AppliedReports,
        Dictionary<string, DateTimeOffset>? Unlocked)
    {
        public static StoredAccountStats From(KeyValuePair<Guid, AccountStats> entry) =>
            new(entry.Key, new(entry.Value.Counters), new(entry.Value.Maxima), [.. entry.Value.AppliedReports], new(entry.Value.Unlocked));

        public AccountStats ToStats() => AccountStats.Rehydrate(Counters, Maxima, AppliedReports, Unlocked);
    }
}
