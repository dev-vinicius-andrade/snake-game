using Application.Manager.Api.Accounts;
using Library.Commons.Game.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Manager.Api.Persistence;

/// <summary>What <see cref="DatabaseImporter.Import"/> did. <paramref name="Skipped"/>: the database already had accounts, so the files were left alone.</summary>
public sealed record ImportResult(int Accounts, int Stats, bool Skipped = false);

/// <summary>
/// Moves what the manager kept in JSON files (the accounts, and their stats and unlocks) into an empty database, so a manager that is given a
/// connection string does not start without its players. It only runs on a database with no accounts, and does everything in one transaction:
/// either all of it is in, or none, and a run that failed is run again from the start. The files are only read, never changed or deleted
/// (a file that cannot be read is put aside by the file store, as it always was).
/// </summary>
public static class DatabaseImporter
{
    public static ImportResult Import(IDbContextFactory<SnakeDbContext> factory, IReadOnlyList<AchievementDefinition> definitions, string? accountsPath,
        string? statsPath, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(definitions);
        if (string.IsNullOrWhiteSpace(accountsPath) || !File.Exists(accountsPath))
            return new ImportResult(0, 0);
        AchievementCatalog.Sync(factory, definitions);
        using var context = factory.CreateDbContext();
        if (context.Accounts.Any())
        {
            logger?.LogInformation("The database has accounts already: the file {Path} was not imported", accountsPath);
            return new ImportResult(0, 0, Skipped: true);
        }
        var accounts = new FileAccountStore(accountsPath, logger).All();
        var stats = !string.IsNullOrWhiteSpace(statsPath) && File.Exists(statsPath) ? new FileStatsStore(statsPath, logger).All() : new Dictionary<Guid, AccountStats>();
        using var transaction = context.Database.BeginTransaction();
        context.Accounts.AddRange(accounts.Select(EfAccountStore.ToRow));
        var skipped = stats.Sum(entry => EfStatsStore.AddImported(context, entry.Key, entry.Value, definitions));
        context.SaveChanges();
        transaction.Commit();
        logger?.LogInformation("Imported {Accounts} accounts and the stats of {Stats} from the files into the database ({Skipped} unlocks of achievements that no longer exist were left out)",
            accounts.Count, stats.Count, skipped);
        return new ImportResult(accounts.Count, stats.Count);
    }
}
