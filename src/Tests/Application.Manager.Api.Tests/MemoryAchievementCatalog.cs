using Application.Manager.Api.Accounts;

namespace Application.Manager.Api.Tests;

/// <summary>A catalog in memory that can be changed, empty to begin with. Tests use it where the real one would need a database.</summary>
public sealed class MemoryAchievementCatalog : IAchievementCatalog
{
    private readonly List<AchievementEntry> _entries = [];
    private readonly HashSet<string> _inUse = [];

    public bool IsWritable => true;

    /// <summary>Says that somebody has unlocked this achievement, so it cannot be deleted.</summary>
    public void MarkInUse(string id) => _inUse.Add(id);

    public IReadOnlyList<AchievementEntry> All() => _entries.ToList();

    public AchievementChange Save(AchievementEntry entry)
    {
        var at = _entries.FindIndex(existing => existing.Id == entry.Id);
        if (at < 0) _entries.Add(entry);
        else _entries[at] = entry;
        return AchievementChange.Saved;
    }

    public AchievementChange Delete(string id)
    {
        if (_inUse.Contains(id)) return AchievementChange.InUse;
        return _entries.RemoveAll(entry => entry.Id == id) > 0 ? AchievementChange.Deleted : AchievementChange.NotFound;
    }
}
