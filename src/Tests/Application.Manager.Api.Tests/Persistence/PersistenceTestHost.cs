using Application.Manager.Api.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Application.Manager.Api.Tests.Persistence;

/// <summary>
/// A throwaway database for the EF stores: SQLite in memory, made from the model (no migrations: those are Postgres). The keeper connection
/// keeps the named in memory database alive; every context opens a connection of its own, like it does against Postgres.
/// </summary>
public sealed class PersistenceTestHost : IDisposable
{
    private readonly SqliteConnection _keeper;
    private readonly DbContextOptions<SnakeDbContext> _options;

    public PersistenceTestHost()
    {
        var connectionString = $"Data Source=file:snake-{Guid.NewGuid():N}?mode=memory&cache=shared";
        _keeper = new SqliteConnection(connectionString);
        _keeper.Open();
        _options = new DbContextOptionsBuilder<SnakeDbContext>().UseSqlite(connectionString).Options;
        using var context = new SnakeDbContext(_options);
        context.Database.EnsureCreated();
    }

    public IDbContextFactory<SnakeDbContext> Factory => new OptionsFactory(_options);

    public EfAccountStore Accounts() => new(Factory);

    public EfStatsStore Stats() => new(Factory);

    public EfRoleStore Roles() => new(Factory);

    public void Dispose() => _keeper.Dispose();

    private sealed class OptionsFactory(DbContextOptions<SnakeDbContext> options) : IDbContextFactory<SnakeDbContext>
    {
        public SnakeDbContext CreateDbContext() => new(options);
    }
}
