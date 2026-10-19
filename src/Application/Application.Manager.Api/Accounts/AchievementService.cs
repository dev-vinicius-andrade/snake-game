using Library.Commons.Game.Domain.Entities;

namespace Application.Manager.Api.Accounts;

/// <summary>
/// Stats and achievements of signed-in players (docs/ACHIEVEMENTS.md): adds up what a game server saw in one life and unlocks what that reaches,
/// and shows a player its profile. Expected failures are results with a <see cref="StatsErrors"/> code, never exceptions. Only the store throws
/// (when it cannot save).
/// </summary>
public sealed class AchievementService
{
    private readonly IAccountStore _accounts;
    private readonly IStatsStore _stats;
    private readonly TimeProvider _time;
    private readonly ILogger<AchievementService>? _logger;
    private readonly IReadOnlyDictionary<string, int> _definitionOrder;

    /// <param name="definitions">The achievements that can be unlocked; <see cref="Achievements.Defaults"/> when null.</param>
    public AchievementService(IAccountStore accounts, IStatsStore stats, TimeProvider time, IReadOnlyList<AchievementDefinition>? definitions = null,
        ILogger<AchievementService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(accounts);
        ArgumentNullException.ThrowIfNull(stats);
        ArgumentNullException.ThrowIfNull(time);
        _accounts = accounts;
        _stats = stats;
        _time = time;
        _logger = logger;
        Definitions = definitions ?? Achievements.Defaults;
        _definitionOrder = Definitions
            .Select((definition, index) => KeyValuePair.Create(definition.Id, index))
            .DistinctBy(entry => entry.Key, StringComparer.Ordinal)
            .ToDictionary(StringComparer.Ordinal);
    }

    public IReadOnlyList<AchievementDefinition> Definitions { get; }

    /// <summary>
    /// Adds one life's numbers to the account and answers with the achievements that unlocked. A report id already applied succeeds with
    /// nothing unlocked. Fails with <see cref="StatsErrors.UnknownAccount"/>, or <see cref="StatsErrors.InvalidStats"/> when there is no report id
    /// or a known value is out of range (unknown keys are ignored).
    /// </summary>
    public AccountResult<UnlockedAchievements> Report(Guid accountId, Guid? reportId, IReadOnlyDictionary<string, long>? counters,
        IReadOnlyDictionary<string, long>? maxima)
    {
        if (_accounts.Find(accountId) is null)
            return AccountResult<UnlockedAchievements>.Failure(StatsErrors.UnknownAccount);
        if (reportId is not Guid id)
            return Refuse(accountId, Guid.Empty);
        var applied = _stats.Apply(accountId, id, counters, maxima, _time.GetUtcNow(), Definitions);
        return applied.Status == StatsApplyStatus.Rejected ? Refuse(accountId, id) : Unlocked(accountId, applied.Unlocked);
    }

    /// <summary>The profile of the account, or null when there is no such account.</summary>
    public AccountProfile? Profile(Guid accountId)
    {
        var account = _accounts.Find(accountId);
        if (account is null)
            return null;
        var stats = _stats.Get(accountId);
        return new AccountProfile(account, EveryStat(stats), UnlockedInOrder(stats));
    }

    private AccountResult<UnlockedAchievements> Refuse(Guid accountId, Guid reportId)
    {
        _logger?.LogWarning("Stats report {ReportId} for account {AccountId} refused: no id or a value out of range", reportId, accountId);
        return AccountResult<UnlockedAchievements>.Failure(StatsErrors.InvalidStats);
    }

    private AccountResult<UnlockedAchievements> Unlocked(Guid accountId, IReadOnlyList<string> unlocked)
    {
        if (unlocked.Count > 0)
            _logger?.LogInformation("Account {AccountId} unlocked {Achievements}", accountId, unlocked);
        return AccountResult<UnlockedAchievements>.Success(new UnlockedAchievements(unlocked));
    }

    private static IReadOnlyDictionary<string, long> EveryStat(AccountStats stats)
        => StatKeys.Counters.Concat(StatKeys.Maxima).ToDictionary(key => key, stats.ValueOf, StringComparer.Ordinal).AsReadOnly();

    private IReadOnlyList<UnlockedAchievement> UnlockedInOrder(AccountStats stats)
        => stats.Unlocked
            .OrderBy(unlock => unlock.Value)
            .ThenBy(unlock => DefinitionOrder(unlock.Key))
            .ThenBy(unlock => unlock.Key, StringComparer.Ordinal)
            .Select(unlock => new UnlockedAchievement(unlock.Key, unlock.Value))
            .ToList()
            .AsReadOnly();

    private int DefinitionOrder(string achievementId) => _definitionOrder.GetValueOrDefault(achievementId, int.MaxValue);
}
