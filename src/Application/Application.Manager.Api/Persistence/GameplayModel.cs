using Microsoft.EntityFrameworkCore;

namespace Application.Manager.Api.Persistence;

/// <summary>The tables of the gameplay config: keys, sizes and the links from prizes and the boss to the perks they give.</summary>
internal static class GameplayModel
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<GameplaySettingsRow>(settings =>
        {
            settings.ToTable("gameplay_settings");
            settings.HasKey(row => row.Id);
            settings.Property(row => row.Id).ValueGeneratedNever();
            settings.Property(row => row.Revision).HasMaxLength(64).IsRequired();
        });
        model.Entity<PerkRow>(perk =>
        {
            perk.ToTable("perks");
            perk.HasKey(row => row.Id);
            perk.Property(row => row.Id).HasMaxLength(SnakeDbContext.MaxName);
            perk.Property(row => row.Name).HasMaxLength(SnakeDbContext.MaxName).IsRequired();
            perk.Property(row => row.Icon).HasMaxLength(SnakeDbContext.MaxName * 2);
            perk.Property(row => row.Description).HasMaxLength(1000);
        });
        model.Entity<PerkEffectRow>(effect =>
        {
            effect.ToTable("perk_effects");
            effect.HasKey(row => new { row.PerkId, row.Position });
            effect.Property(row => row.PerkId).HasMaxLength(SnakeDbContext.MaxName);
            effect.Property(row => row.Effect).HasMaxLength(SnakeDbContext.MaxName).IsRequired();
            effect.Property(row => row.Target).HasMaxLength(16);
            effect.HasOne<PerkRow>().WithMany().HasForeignKey(row => row.PerkId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<PerkTierRow>(tier =>
        {
            tier.ToTable("perk_tiers");
            tier.HasKey(row => new { row.PerkId, row.Position });
            tier.Property(row => row.PerkId).HasMaxLength(SnakeDbContext.MaxName);
            tier.HasOne<PerkRow>().WithMany().HasForeignKey(row => row.PerkId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<PerkRoomRow>(room =>
        {
            room.ToTable("perk_rooms");
            room.HasKey(row => new { row.PerkId, row.IsExcluded, row.Position });
            room.Property(row => row.PerkId).HasMaxLength(SnakeDbContext.MaxName);
            room.Property(row => row.Room).HasMaxLength(SnakeDbContext.MaxName * 2).IsRequired();
            room.HasOne<PerkRow>().WithMany().HasForeignKey(row => row.PerkId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<PrizeRow>(prize =>
        {
            prize.ToTable("prizes");
            prize.HasKey(row => row.Id);
            prize.Property(row => row.Id).HasMaxLength(SnakeDbContext.MaxName);
            prize.Property(row => row.Name).HasMaxLength(SnakeDbContext.MaxName).IsRequired();
            prize.Property(row => row.GrantsPerk).HasMaxLength(SnakeDbContext.MaxName);
            prize.Property(row => row.Color).HasMaxLength(16).IsRequired();
            prize.HasOne<PerkRow>().WithMany().HasForeignKey(row => row.GrantsPerk).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<PrizeRoomRow>(room =>
        {
            room.ToTable("prize_rooms");
            room.HasKey(row => new { row.PrizeId, row.IsExcluded, row.Position });
            room.Property(row => row.PrizeId).HasMaxLength(SnakeDbContext.MaxName);
            room.Property(row => row.Room).HasMaxLength(SnakeDbContext.MaxName * 2).IsRequired();
            room.HasOne<PrizeRow>().WithMany().HasForeignKey(row => row.PrizeId).OnDelete(DeleteBehavior.Cascade);
        });
        model.Entity<RewardRow>(reward =>
        {
            reward.ToTable("rewards");
            reward.HasKey(row => row.Id);
            reward.Property(row => row.Id).HasMaxLength(SnakeDbContext.MaxName);
            reward.Property(row => row.Name).HasMaxLength(SnakeDbContext.MaxName).IsRequired();
            reward.Property(row => row.TierSurgePerk).HasMaxLength(SnakeDbContext.MaxName);
        });
        model.Entity<PrizeRewardRow>(link =>
        {
            link.ToTable("prize_rewards");
            link.HasKey(row => new { row.PrizeId, row.Position });
            link.Property(row => row.PrizeId).HasMaxLength(SnakeDbContext.MaxName);
            link.Property(row => row.RewardId).HasMaxLength(SnakeDbContext.MaxName).IsRequired();
            link.HasOne<PrizeRow>().WithMany().HasForeignKey(row => row.PrizeId).OnDelete(DeleteBehavior.Cascade);
            link.HasOne<RewardRow>().WithMany().HasForeignKey(row => row.RewardId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<WorldEventRewardRow>(link =>
        {
            link.ToTable("world_event_rewards");
            link.HasKey(row => new { row.Event, row.Position });
            link.Property(row => row.Event).HasMaxLength(32);
            link.Property(row => row.RewardId).HasMaxLength(SnakeDbContext.MaxName).IsRequired();
            link.HasOne<RewardRow>().WithMany().HasForeignKey(row => row.RewardId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<LightningEventRow>(lightning =>
        {
            lightning.ToTable("world_event_lightning");
            lightning.HasKey(row => row.Id);
            lightning.Property(row => row.Id).ValueGeneratedNever();
        });
        model.Entity<MeteorEventRow>(meteors =>
        {
            meteors.ToTable("world_event_meteors");
            meteors.HasKey(row => row.Id);
            meteors.Property(row => row.Id).ValueGeneratedNever();
        });
        model.Entity<ZoneEventRow>(zone =>
        {
            zone.ToTable("world_event_zone");
            zone.HasKey(row => row.Id);
            zone.Property(row => row.Id).ValueGeneratedNever();
        });
        SingleRow<FruitShowerEventRow>(model, "world_event_fruit_shower");
        SingleRow<BountyEventRow>(model, "world_event_bounty");
        SingleRow<MineFieldEventRow>(model, "world_event_mine_field");
        SingleRow<SugarRushEventRow>(model, "world_event_sugar_rush");
        SingleRow<MazeEventRow>(model, "world_event_maze");
        model.Entity<MazeEventRow>(maze =>
        {
            maze.Property(row => row.PrizeGrantsPerk).HasMaxLength(SnakeDbContext.MaxName);
            maze.Property(row => row.PrizeColor).HasMaxLength(7).IsRequired();
            maze.HasOne<PerkRow>().WithMany().HasForeignKey(row => row.PrizeGrantsPerk).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<BossRow>(boss =>
        {
            boss.ToTable("bosses");
            boss.HasKey(row => row.Id);
            boss.Property(row => row.Id).HasMaxLength(SnakeDbContext.MaxName);
            boss.Property(row => row.GrantsPerk).HasMaxLength(SnakeDbContext.MaxName);
            boss.HasOne<PerkRow>().WithMany().HasForeignKey(row => row.GrantsPerk).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<WorldEventRoomRow>(room =>
        {
            room.ToTable("world_event_rooms");
            room.HasKey(row => new { row.Event, row.IsExcluded, row.Position });
            room.Property(row => row.Event).HasMaxLength(32);
            room.Property(row => row.Room).HasMaxLength(SnakeDbContext.MaxName * 2).IsRequired();
        });
    }

    /// <summary>A one-row event table keyed by an <c>Id</c> the code sets (always 1).</summary>
    private static void SingleRow<TRow>(ModelBuilder model, string table) where TRow : class
        => model.Entity<TRow>(row =>
        {
            row.ToTable(table);
            row.HasKey("Id");
            row.Property("Id").ValueGeneratedNever();
        });
}
