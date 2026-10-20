using Application.Manager.Api.Accounts;
using Application.Manager.Api.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Application.Manager.Api.Tests.Persistence;

public sealed class DatabaseWiringTests
{
    private const string Connection = "Host=db.invalid;Database=snake;Username=snake;Password=not-a-real-password";

    private static ServiceProvider Provider(string? connection, bool migrateOnStart = true)
    {
        var settings = new Dictionary<string, string?> { [DatabaseServiceCollectionExtensions.ConnectionKey] = connection };
        if (!migrateOnStart) settings[DatabaseServiceCollectionExtensions.MigrateOnStartKey] = "false";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        return new ServiceCollection().AddLogging().AddAccounts(configuration).BuildServiceProvider();
    }

    [Fact]
    public void WithAConnectionString_AccountsAndStatsLiveInTheDatabase()
    {
        using var provider = Provider(Connection);

        Assert.IsType<EfAccountStore>(provider.GetRequiredService<IAccountStore>());
        Assert.IsType<EfStatsStore>(provider.GetRequiredService<IStatsStore>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WithoutAConnectionString_NothingChanges_AndNoDatabaseIsTouched(string? connection)
    {
        using var provider = Provider(connection);

        Assert.IsType<FileAccountStore>(provider.GetRequiredService<IAccountStore>());
        Assert.IsType<FileStatsStore>(provider.GetRequiredService<IStatsStore>());
        Assert.DoesNotContain(provider.GetServices<Microsoft.Extensions.Hosting.IHostedService>(), service => service is DatabaseMigrationService);
    }

    [Fact]
    public void TheDatabaseIsMadeReadyAtStart_UnlessSwitchedOff()
    {
        using var on = Provider(Connection);
        using var off = Provider(Connection, migrateOnStart: false);

        Assert.Contains(on.GetServices<Microsoft.Extensions.Hosting.IHostedService>(), service => service is DatabaseMigrationService);
        Assert.DoesNotContain(off.GetServices<Microsoft.Extensions.Hosting.IHostedService>(), service => service is DatabaseMigrationService);
    }

    [Fact]
    public void TheMigrationsMatchTheModel_SoNoChangeToTheModelWasLeftWithoutAMigration()
    {
        var options = new DbContextOptionsBuilder<SnakeDbContext>().UseNpgsql(Connection).Options;
        using var context = new SnakeDbContext(options);
        var assembly = context.GetService<IMigrationsAssembly>();
        var differ = context.GetService<IMigrationsModelDiffer>();
        var snapshot = assembly.ModelSnapshot?.Model;
        Assert.NotNull(snapshot);
        var finalized = context.GetService<IModelRuntimeInitializer>().Initialize(snapshot);

        var differences = differ.GetDifferences(finalized.GetRelationalModel(), context.GetService<IDesignTimeModel>().Model.GetRelationalModel());

        Assert.Empty(differences);
    }

    [Fact]
    public void ThereIsAtLeastTheInitialMigration()
    {
        var options = new DbContextOptionsBuilder<SnakeDbContext>().UseNpgsql(Connection).Options;
        using var context = new SnakeDbContext(options);

        Assert.NotEmpty(context.GetService<IMigrationsAssembly>().Migrations);
    }
}
