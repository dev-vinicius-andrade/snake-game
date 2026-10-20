using Library.Commons.Game.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Manager.Api.Persistence;

/// <summary>
/// Puts the achievements of the code in the <c>achievements</c> table that are not there yet. Nothing already in the table is touched: from then
/// on the table is the source, and an administrator may have changed it (docs/ACHIEVEMENTS.md). Safe to run any number of times, on any number
/// of manager instances.
/// </summary>
public static class AchievementCatalog
{
    public static void Sync(IDbContextFactory<SnakeDbContext> factory, IReadOnlyList<AchievementDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(definitions);
        using var context = factory.CreateDbContext();
        var known = context.Achievements.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
        var next = context.Achievements.Any() ? context.Achievements.Max(row => row.SortOrder) + 1 : 0;
        foreach (var definition in definitions.Where(definition => !known.Contains(definition.Id)))
            context.Achievements.Add(ToRow(definition, next++));
        if (context.ChangeTracker.HasChanges())
            Save(context);
    }

    internal static AchievementRow ToRow(AchievementDefinition definition, int order)
        => new() { Id = definition.Id, Icon = definition.Icon, Stat = definition.Stat, Threshold = definition.Threshold, SortOrder = order };

    private static void Save(SnakeDbContext context)
    {
        try
        {
            context.SaveChanges();
        }
        catch (DbUpdateException)
        {
            // Another instance added the same rows first: they are there now, which is all this needs.
        }
    }
}
