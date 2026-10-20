using Application.Manager.Api.Accounts;
using Microsoft.EntityFrameworkCore;

namespace Application.Manager.Api.Persistence;

/// <summary>
/// The achievements of the game in the database (<c>achievements</c> and <c>achievement_texts</c>), changeable from the admin page. What was read
/// is kept for <see cref="RefreshEvery"/>, since every stats report asks what can be unlocked, and a change made here is seen at once; one made on
/// another manager is seen when the time is up.
/// </summary>
public sealed class EfAchievementCatalog(IDbContextFactory<SnakeDbContext> factory, TimeProvider time) : IAchievementCatalog
{
    public static readonly TimeSpan RefreshEvery = TimeSpan.FromSeconds(5);

    private readonly object _lock = new();
    private IReadOnlyList<AchievementEntry>? _cached;
    private DateTimeOffset _readAt;

    public bool IsWritable => true;

    public IReadOnlyList<AchievementEntry> All()
    {
        lock (_lock)
        {
            if (_cached is null || time.GetUtcNow() - _readAt >= RefreshEvery)
                Read();
            return _cached!;
        }
    }

    public AchievementChange Save(AchievementEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_lock)
        {
            using var context = factory.CreateDbContext();
            using var transaction = context.Database.BeginTransaction();
            var row = context.Achievements.SingleOrDefault(existing => existing.Id == entry.Id);
            if (row is null)
            {
                var next = context.Achievements.Any() ? context.Achievements.Max(existing => existing.SortOrder) + 1 : 0;
                row = new AchievementRow { Id = entry.Id, SortOrder = next };
                context.Achievements.Add(row);
            }
            row.Icon = entry.Icon;
            row.Stat = entry.Stat;
            row.Threshold = entry.Threshold;
            row.Enabled = entry.Enabled;
            context.SaveChanges();
            ReplaceTexts(context, entry);
            context.SaveChanges();
            transaction.Commit();
            _cached = null;
            return AchievementChange.Saved;
        }
    }

    public AchievementChange Delete(string id)
    {
        lock (_lock)
        {
            using var context = factory.CreateDbContext();
            if (!context.Achievements.Any(row => row.Id == id)) return AchievementChange.NotFound;
            if (context.UnlockedAchievements.Any(row => row.AchievementId == id)) return AchievementChange.InUse;
            context.AchievementTexts.Where(row => row.AchievementId == id).ExecuteDelete();
            context.Achievements.Where(row => row.Id == id).ExecuteDelete();
            _cached = null;
            return AchievementChange.Deleted;
        }
    }

    private static void ReplaceTexts(SnakeDbContext context, AchievementEntry entry)
    {
        context.AchievementTexts.Where(row => row.AchievementId == entry.Id).ExecuteDelete();
        foreach (var (locale, text) in entry.Texts ?? new Dictionary<string, AchievementText>())
            context.AchievementTexts.Add(new AchievementTextRow { AchievementId = entry.Id, Locale = locale, Name = text.Name.Trim(), Description = (text.Description ?? "").Trim() });
    }

    private void Read()
    {
        using var context = factory.CreateDbContext();
        var texts = context.AchievementTexts.AsNoTracking().ToLookup(row => row.AchievementId);
        _cached = context.Achievements.AsNoTracking().OrderBy(row => row.SortOrder).ToList()
            .Select(row => new AchievementEntry(row.Id, row.Icon, row.Stat, row.Threshold, row.Enabled,
                texts[row.Id].ToDictionary(text => text.Locale, text => new AchievementText(text.Name, text.Description))))
            .ToList()
            .AsReadOnly();
        _readAt = time.GetUtcNow();
    }
}
