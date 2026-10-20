using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Application.Manager.Api.Persistence;

/// <summary>
/// Everything the manager keeps in the database: accounts and their stats. Tables and columns are snake_case. The model only uses plain
/// types, so the same model runs on Postgres (migrations in <c>Persistence/Migrations</c>) and, for the tests, on SQLite.
/// Times are kept in UTC: Postgres timestamptz refuses any other offset.
/// </summary>
public sealed class SnakeDbContext(DbContextOptions<SnakeDbContext> options) : DbContext(options)
{
    public const int MaxName = 64;

    public DbSet<AccountRow> Accounts => Set<AccountRow>();

    public DbSet<DeviceRow> Devices => Set<DeviceRow>();

    public DbSet<ExternalLoginRow> ExternalLogins => Set<ExternalLoginRow>();

    public DbSet<StatsHeadRow> StatsHeads => Set<StatsHeadRow>();

    public DbSet<StatValueRow> StatValues => Set<StatValueRow>();

    public DbSet<AppliedReportRow> AppliedReports => Set<AppliedReportRow>();

    public DbSet<GameplaySettingsRow> GameplaySettings => Set<GameplaySettingsRow>();

    public DbSet<PerkRow> Perks => Set<PerkRow>();

    public DbSet<PerkEffectRow> PerkEffects => Set<PerkEffectRow>();

    public DbSet<PerkTierRow> PerkTiers => Set<PerkTierRow>();

    public DbSet<PerkRoomRow> PerkRooms => Set<PerkRoomRow>();

    public DbSet<PrizeRow> Prizes => Set<PrizeRow>();

    public DbSet<PrizeRoomRow> PrizeRooms => Set<PrizeRoomRow>();

    public DbSet<LightningEventRow> LightningEvents => Set<LightningEventRow>();

    public DbSet<MeteorEventRow> MeteorEvents => Set<MeteorEventRow>();

    public DbSet<ZoneEventRow> ZoneEvents => Set<ZoneEventRow>();

    public DbSet<BossRow> Bosses => Set<BossRow>();

    public DbSet<WorldEventRoomRow> WorldEventRooms => Set<WorldEventRoomRow>();

    public DbSet<RoleRow> Roles => Set<RoleRow>();

    public DbSet<AccountRoleRow> AccountRoles => Set<AccountRoleRow>();

    public DbSet<AchievementRow> Achievements => Set<AchievementRow>();

    public DbSet<UnlockedAchievementRow> UnlockedAchievements => Set<UnlockedAchievementRow>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configuration)
        => configuration.Properties<DateTimeOffset>().HaveConversion<UtcDateTimeOffsetConverter>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<AccountRow>(account =>
        {
            account.ToTable("accounts");
            account.HasKey(row => row.Id);
            account.Property(row => row.Username).HasMaxLength(MaxName).IsRequired();
            account.Property(row => row.UsernameKey).HasMaxLength(MaxName).IsRequired();
            account.Property(row => row.PhraseHash).HasMaxLength(256).IsRequired();
            account.Property(row => row.DisplayName).HasMaxLength(MaxName);
            account.HasIndex(row => new { row.UsernameKey, row.Tag }).IsUnique();
            account.HasMany(row => row.Devices).WithOne().HasForeignKey(device => device.AccountId).OnDelete(DeleteBehavior.Cascade);
            account.HasMany(row => row.Logins).WithOne().HasForeignKey(login => login.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<DeviceRow>(device =>
        {
            device.ToTable("devices");
            device.HasKey(row => row.Hash);
            device.Property(row => row.Hash).HasMaxLength(128);
            device.Property(row => row.Label).HasMaxLength(MaxName * 2);
            device.HasIndex(row => row.AccountId);
        });
        model.Entity<ExternalLoginRow>(login =>
        {
            login.ToTable("external_logins");
            login.HasKey(row => new { row.Provider, row.IdHash });
            login.Property(row => row.Provider).HasMaxLength(32);
            login.Property(row => row.IdHash).HasMaxLength(128);
            login.HasIndex(row => new { row.AccountId, row.Provider }).IsUnique();
        });
        model.Entity<StatsHeadRow>(head =>
        {
            head.ToTable("stats_heads");
            head.HasKey(row => row.AccountId);
            head.Property(row => row.Version).IsConcurrencyToken();
        });
        model.Entity<StatValueRow>(value =>
        {
            value.ToTable("stat_values");
            value.HasKey(row => new { row.AccountId, row.Kind, row.Key });
            value.Property(row => row.Key).HasMaxLength(MaxName);
            value.Property(row => row.Kind).HasConversion<int>();
        });
        model.Entity<AppliedReportRow>(report =>
        {
            report.ToTable("applied_reports");
            report.HasKey(row => new { row.AccountId, row.ReportId });
            report.HasIndex(row => new { row.AccountId, row.Sequence });
        });
        model.Entity<AchievementRow>(achievement =>
        {
            achievement.ToTable("achievements");
            achievement.HasKey(row => row.Id);
            achievement.Property(row => row.Id).HasMaxLength(MaxName);
            achievement.Property(row => row.Icon).HasMaxLength(MaxName * 2).IsRequired();
            achievement.Property(row => row.Stat).HasMaxLength(MaxName).IsRequired();
        });
        model.Entity<UnlockedAchievementRow>(unlocked =>
        {
            unlocked.ToTable("unlocked_achievements");
            unlocked.HasKey(row => new { row.AccountId, row.AchievementId });
            unlocked.Property(row => row.AchievementId).HasMaxLength(MaxName);
            unlocked.HasOne<AchievementRow>().WithMany().HasForeignKey(row => row.AchievementId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<RoleRow>(role =>
        {
            role.ToTable("roles");
            role.HasKey(row => row.Name);
            role.Property(row => row.Name).HasMaxLength(32);
            role.HasData(Application.Manager.Api.Accounts.Roles.All.Select(name => new RoleRow { Name = name }));
        });
        model.Entity<AccountRoleRow>(granted =>
        {
            granted.ToTable("account_roles");
            granted.HasKey(row => new { row.AccountId, row.Role });
            granted.Property(row => row.Role).HasMaxLength(32);
            granted.HasOne<AccountRow>().WithMany().HasForeignKey(row => row.AccountId).OnDelete(DeleteBehavior.Cascade);
            granted.HasOne<RoleRow>().WithMany().HasForeignKey(row => row.Role).OnDelete(DeleteBehavior.Restrict);
        });
        GameplayModel.Configure(model);
        UseSnakeCaseNames(model);
    }

    private static void UseSnakeCaseNames(ModelBuilder model)
    {
        foreach (var entity in model.Model.GetEntityTypes())
        {
            var table = entity.GetTableName();
            foreach (var property in entity.GetProperties())
                property.SetColumnName(SnakeCase(property.Name));
            foreach (var key in entity.GetKeys())
                key.SetName($"pk_{table}");
            foreach (var index in entity.GetIndexes())
                index.SetDatabaseName($"{(index.IsUnique ? "ux" : "ix")}_{table}_{string.Join('_', index.Properties.Select(property => SnakeCase(property.Name)))}");
            foreach (var foreignKey in entity.GetForeignKeys())
                foreignKey.SetConstraintName($"fk_{table}_{foreignKey.PrincipalEntityType.GetTableName()}");
        }
    }

    internal static string SnakeCase(string name)
    {
        var text = new StringBuilder(name.Length + 4);
        for (var index = 0; index < name.Length; index++)
        {
            if (char.IsUpper(name[index]) && index > 0) text.Append('_');
            text.Append(char.ToLowerInvariant(name[index]));
        }
        return text.ToString();
    }
}

/// <summary>Stores a time as UTC and gives it back as UTC: the same instant, which is all the game ever asks of a time.</summary>
public sealed class UtcDateTimeOffsetConverter : ValueConverter<DateTimeOffset, DateTimeOffset>
{
    public UtcDateTimeOffsetConverter() : base(time => time.ToUniversalTime(), time => time.ToUniversalTime())
    {
    }
}
