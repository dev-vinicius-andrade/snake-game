using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Application.Manager.Api.Persistence;

/// <summary>
/// What <c>dotnet ef</c> uses to make the context when it adds or applies migrations. It reads the connection string from
/// <c>SNAKE_DB</c>, and falls back to a placeholder, which is enough to add a migration (that never connects) but not to update a database.
/// </summary>
public sealed class SnakeDbContextFactory : IDesignTimeDbContextFactory<SnakeDbContext>
{
    public const string ConnectionVariable = "SNAKE_DB";

    public SnakeDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connection))
            connection = "Host=localhost;Database=snake;Username=snake;Password=design-time-only";
        var options = new DbContextOptionsBuilder<SnakeDbContext>()
            .UseNpgsql(connection, npgsql => npgsql.MigrationsHistoryTable("migrations_history"))
            .Options;
        return new SnakeDbContext(options);
    }
}
