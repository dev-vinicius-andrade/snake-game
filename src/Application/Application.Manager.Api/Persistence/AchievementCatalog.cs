using Library.Commons.Game.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Manager.Api.Persistence;

/// <summary>
/// Keeps the <c>achievements</c> table in step with the definitions in the code: a new definition is added, a changed one (icon, stat,
/// threshold, place in the list) is updated, and one that was removed stays, because players may have unlocked it. Safe to run any number
/// of times, on any number of manager instances.
/// </summary>
public static class AchievementCatalog
{
    public static void Sync(IDbContextFactory<SnakeDbContext> factory, IReadOnlyList<AchievementDefinition> definitions)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(definitions);
        using var context = factory.CreateDbContext();
        var rows = context.Achievements.ToDictionary(row => row.Id, StringComparer.Ordinal);
        for (var order = 0; order < definitions.Count; order++)
            Apply(context, rows, definitions[order], order);
        if (context.ChangeTracker.HasChanges())
            Save(context);
    }

    internal static AchievementRow ToRow(AchievementDefinition definition, int order)
        => new() { Id = definition.Id, Icon = definition.Icon, Stat = definition.Stat, Threshold = definition.Threshold, SortOrder = order };

    private static void Apply(SnakeDbContext context, Dictionary<string, AchievementRow> rows, AchievementDefinition definition, int order)
    {
        if (!rows.TryGetValue(definition.Id, out var row))
        {
            context.Achievements.Add(ToRow(definition, order));
            return;
        }
        row.Icon = definition.Icon;
        row.Stat = definition.Stat;
        row.Threshold = definition.Threshold;
        row.SortOrder = order;
    }

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
