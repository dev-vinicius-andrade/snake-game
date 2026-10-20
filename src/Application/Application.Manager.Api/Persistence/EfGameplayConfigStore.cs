using Application.Manager.Api.Gameplay;
using Domain.Game.Rules;
using Microsoft.EntityFrameworkCore;

namespace Application.Manager.Api.Persistence;

/// <summary>
/// The gameplay config in the database tables (perks, prizes, world events, the boss, room lifetimes: see docs/DATABASE.md). The config in
/// force is kept in memory and looked at again when <see cref="RefreshEvery"/> has passed, by its revision only, so a change made on another
/// manager (or by hand in the tables, with a new revision) reaches this one soon. A save checks the config as the file store does, then replaces
/// every row in one transaction. An empty database is filled from the seed (the old gameplay file, when there is one) or the defaults.
/// </summary>
public sealed class EfGameplayConfigStore(IDbContextFactory<SnakeDbContext> factory, TimeProvider time, Func<GameplayConfig>? seed = null, ILogger? logger = null)
    : IGameplayConfigStore
{
    /// <summary>How long what was read stays in use before the revision in the database is looked at again.</summary>
    public static readonly TimeSpan RefreshEvery = TimeSpan.FromSeconds(5);

    private readonly object _lock = new();
    private GameplayDocument? _current;
    private DateTimeOffset _checkedAt;

    public GameplayDocument Current
    {
        get
        {
            lock (_lock)
            {
                if (_current is null || time.GetUtcNow() - _checkedAt >= RefreshEvery)
                    Refresh();
                return _current!;
            }
        }
    }

    public GameplayDocument Update(GameplayConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var complete = GameplayConfigStore.Prepare(config);
        lock (_lock)
        {
            var document = new GameplayDocument(NewRevision(), complete);
            Save(document);
            _current = document;
            _checkedAt = time.GetUtcNow();
            return document;
        }
    }

    private void Refresh()
    {
        using var context = factory.CreateDbContext();
        var revision = GameplayConfigMapper.ReadRevision(context);
        _checkedAt = time.GetUtcNow();
        if (revision is null)
            _current = SeedAndRead();
        else if (_current is null || _current.Revision != revision)
            _current = GameplayConfigMapper.Read(context) ?? SeedAndRead();
    }

    private GameplayDocument SeedAndRead()
    {
        var config = GameplayConfigStore.Prepare((seed ?? (() => GameplayConfig.Defaults))());
        var document = new GameplayDocument(NewRevision(), config);
        try
        {
            Save(document);
            logger?.LogInformation("The gameplay config tables were empty and were filled in (revision {Revision})", document.Revision);
            return document;
        }
        catch (DbUpdateException)
        {
            // Another manager filled them first: use what it saved.
            using var context = factory.CreateDbContext();
            return GameplayConfigMapper.Read(context) ?? throw new InvalidOperationException("The gameplay config could not be saved or read.");
        }
    }

    private void Save(GameplayDocument document)
    {
        using var context = factory.CreateDbContext();
        using var transaction = context.Database.BeginTransaction();
        GameplayConfigMapper.Clear(context);
        GameplayConfigMapper.Add(context, document.Config, document.Revision, time.GetUtcNow());
        context.SaveChanges();
        transaction.Commit();
    }

    private static string NewRevision() => Guid.NewGuid().ToString("N");
}
