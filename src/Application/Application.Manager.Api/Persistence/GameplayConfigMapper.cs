using Domain.Game.Rules;
using Library.Commons.Game.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Manager.Api.Persistence;

/// <summary>Moves a <see cref="GameplayConfig"/> to its tables and back. A save replaces every row; a read builds the whole config.</summary>
internal static class GameplayConfigMapper
{
    private const string Lightning = "lightning";
    private const string Meteors = "meteors";
    private const string Zone = "zone";
    private const string FruitShower = "fruit_shower";
    private const string Bounty = "bounty";
    private const string MineField = "mine_field";
    private const string SugarRush = "sugar_rush";
    private const string Maze = "maze";
    private const string Turret = BossRow.Turret;

    /// <summary>Empties every gameplay table, children before parents, so what is added next can reuse the same keys.</summary>
    public static void Clear(SnakeDbContext context)
    {
        context.WorldEventRewards.ExecuteDelete();
        context.PrizeRewards.ExecuteDelete();
        context.Rewards.ExecuteDelete();
        context.WorldEventRooms.ExecuteDelete();
        context.PerkRooms.ExecuteDelete();
        context.PerkTiers.ExecuteDelete();
        context.PerkEffects.ExecuteDelete();
        context.PrizeRooms.ExecuteDelete();
        context.Prizes.ExecuteDelete();
        context.Bosses.ExecuteDelete();
        // The maze prize may point at a perk, so the maze goes before the perks, like the bosses.
        context.MazeEvents.ExecuteDelete();
        context.Perks.ExecuteDelete();
        context.LightningEvents.ExecuteDelete();
        context.MeteorEvents.ExecuteDelete();
        context.ZoneEvents.ExecuteDelete();
        context.FruitShowerEvents.ExecuteDelete();
        context.BountyEvents.ExecuteDelete();
        context.MineFieldEvents.ExecuteDelete();
        context.SugarRushEvents.ExecuteDelete();
        context.GameplaySettings.ExecuteDelete();
    }

    public static void Add(SnakeDbContext context, GameplayConfig config, string revision, DateTimeOffset now)
    {
        context.GameplaySettings.Add(new GameplaySettingsRow
        {
            Revision = revision,
            UpdatedAt = now,
            PerksEnabled = config.Perks.Enabled,
            ShotSpreadDegrees = config.Perks.ShotSpreadDegrees,
            BurstIntervalMilliseconds = config.Perks.BurstIntervalMilliseconds,
            KillFoodsCredit = config.Perks.KillFoodsCredit,
            ExtraLifeKeepShare = config.Perks.ExtraLifeKeepShare,
            ExtraLifeProtectSeconds = config.Perks.ExtraLifeProtectSeconds,
            PrizesEnabled = config.Prize.Enabled,
            EmptyPrivateRoomMinutes = config.Rooms.EmptyPrivateRoomMinutes,
            MaxPrivateRoomMinutes = config.Rooms.MaxPrivateRoomMinutes,
            WorldEventsEnabled = config.WorldEvents.Enabled,
        });
        AddRewards(context, config.Rewards);
        AddPerks(context, config.Perks.Perks);
        AddPrizes(context, config.Prize.Prizes);
        AddEvents(context, config.WorldEvents);
    }

    private static void AddRewards(SnakeDbContext context, List<RewardDefinition> rewards)
    {
        for (var order = 0; order < rewards.Count; order++)
        {
            var (id, name, reward) = (rewards[order].Id, rewards[order].Name, rewards[order].Reward);
            context.Rewards.Add(new RewardRow
            {
                Id = id, Name = name, GrantsRandomPerk = reward.GrantsRandomPerk, ExtraLives = reward.ExtraLives, TierSurgeMin = reward.TierSurgeMin,
                TierSurgeMax = reward.TierSurgeMax, TierSurgePerk = NullIfBlank(reward.TierSurgePerk), BonusMass = reward.BonusMass, Mend = reward.Mend,
                SortOrder = order,
            });
        }
    }

    private static void AddPerks(SnakeDbContext context, List<PerkDefinition> perks)
    {
        for (var order = 0; order < perks.Count; order++)
        {
            var perk = perks[order];
            context.Perks.Add(new PerkRow
            {
                Id = perk.Id, Name = perk.Name, FoodsRequired = perk.FoodsRequired, Enabled = perk.Enabled, Icon = perk.Icon,
                Description = perk.Description, Script = perk.Script, CooldownSeconds = perk.CooldownSeconds, DurationSeconds = perk.DurationSeconds,
                SortOrder = order,
            });
            context.PerkEffects.AddRange(perk.Effects.Select((effect, position) =>
                new PerkEffectRow { PerkId = perk.Id, Position = position, Effect = effect.Effect.ToString(), Amount = effect.Amount, Target = NullIfBlank(effect.Target) }));
            context.PerkTiers.AddRange(perk.Tiers.Select((tier, position) =>
                new PerkTierRow { PerkId = perk.Id, Position = position, FoodsRequired = tier.FoodsRequired, Multiplier = tier.Multiplier, RewardOnly = tier.RewardOnly }));
            context.PerkRooms.AddRange(Rooms(perk.Rooms, perk.ExcludedRooms, (room, excluded, position) =>
                new PerkRoomRow { PerkId = perk.Id, IsExcluded = excluded, Position = position, Room = room }));
        }
    }

    private static void AddPrizes(SnakeDbContext context, List<PrizeDefinition> prizes)
    {
        for (var order = 0; order < prizes.Count; order++)
        {
            var prize = prizes[order];
            var row = new PrizeRow
            {
                Id = prize.Id, Name = prize.Name, Enabled = prize.Enabled, SpawnIntervalSeconds = prize.SpawnIntervalSeconds,
                LifetimeSeconds = prize.LifetimeSeconds, Value = prize.Value, FoodsCredit = prize.FoodsCredit, MinPlayers = prize.MinPlayers,
                GrantsPerk = NullIfBlank(prize.GrantsPerk), Color = prize.Color, SortOrder = order,
            };
            context.Prizes.Add(row);
            context.PrizeRewards.AddRange(prize.RewardIds.Select((rewardId, position) => new PrizeRewardRow { PrizeId = prize.Id, Position = position, RewardId = rewardId }));
            context.PrizeRooms.AddRange(Rooms(prize.Rooms, prize.ExcludedRooms, (room, excluded, position) =>
                new PrizeRoomRow { PrizeId = prize.Id, IsExcluded = excluded, Position = position, Room = room }));
        }
    }

    private static void AddEvents(SnakeDbContext context, WorldEventOptions events)
    {
        var (lightning, meteors, turret, zone) = (events.Lightning, events.Meteors, events.Turret, events.Zone);
        context.LightningEvents.Add(new LightningEventRow
        {
            Enabled = lightning.Enabled, MinIntervalSeconds = lightning.MinIntervalSeconds, MaxIntervalSeconds = lightning.MaxIntervalSeconds,
            Radius = lightning.Radius, TelegraphMilliseconds = lightning.TelegraphMilliseconds, MinStrikes = lightning.MinStrikes,
            MaxStrikes = lightning.MaxStrikes, StormSeconds = lightning.StormSeconds, AimedShare = lightning.AimedShare,
        });
        context.MeteorEvents.Add(new MeteorEventRow
        {
            Enabled = meteors.Enabled, MinIntervalSeconds = meteors.MinIntervalSeconds, MaxIntervalSeconds = meteors.MaxIntervalSeconds,
            Radius = meteors.Radius, TelegraphMilliseconds = meteors.TelegraphMilliseconds, MinMeteors = meteors.MinMeteors,
            MaxMeteors = meteors.MaxMeteors, RainSeconds = meteors.RainSeconds, AimedShare = meteors.AimedShare,
        });
        context.ZoneEvents.Add(new ZoneEventRow
        {
            Enabled = zone.Enabled, MinIntervalSeconds = zone.MinIntervalSeconds, MaxIntervalSeconds = zone.MaxIntervalSeconds,
            WarningSeconds = zone.WarningSeconds, ShrinkSeconds = zone.ShrinkSeconds, HoldSeconds = zone.HoldSeconds, FinalRadius = zone.FinalRadius,
            DamageIntervalMilliseconds = zone.DamageIntervalMilliseconds, DamageMass = zone.DamageMass, MinPlayers = zone.MinPlayers,
        });
        var boss = new BossRow
        {
            Id = Turret, Enabled = turret.Enabled, MinIntervalSeconds = turret.MinIntervalSeconds, MaxIntervalSeconds = turret.MaxIntervalSeconds,
            LifetimeSeconds = turret.LifetimeSeconds, HitPoints = turret.HitPoints, Size = turret.Size, FireIntervalMilliseconds = turret.FireIntervalMilliseconds,
            Arms = turret.Arms, SpinDegrees = turret.SpinDegrees, ShotSpeed = turret.ShotSpeed, MinPlayers = turret.MinPlayers,
            RewardMass = turret.RewardMass, GrantsPerk = NullIfBlank(turret.GrantsPerk), FoodsCredit = turret.FoodsCredit,
        };
        context.Bosses.Add(boss);
        context.WorldEventRewards.AddRange(EventRewards(Turret, turret.RewardIds));
        context.WorldEventRooms.AddRange(EventRooms(Lightning, lightning.Rooms, lightning.ExcludedRooms));
        context.WorldEventRooms.AddRange(EventRooms(Meteors, meteors.Rooms, meteors.ExcludedRooms));
        context.WorldEventRooms.AddRange(EventRooms(Zone, zone.Rooms, zone.ExcludedRooms));
        context.WorldEventRooms.AddRange(EventRooms(Turret, turret.Rooms, turret.ExcludedRooms));
        AddScheduledEvents(context, events);
    }

    private static void AddScheduledEvents(SnakeDbContext context, WorldEventOptions events)
    {
        var (shower, bounty, mines, rush) = (events.FruitShower, events.Bounty, events.Mines, events.SugarRush);
        context.FruitShowerEvents.Add(new FruitShowerEventRow
        {
            Enabled = shower.Enabled, MinIntervalSeconds = shower.MinIntervalSeconds, MaxIntervalSeconds = shower.MaxIntervalSeconds,
            DurationSeconds = shower.DurationSeconds, FruitsPerSecond = shower.FruitsPerSecond, MinPlayers = shower.MinPlayers,
        });
        var bountyRow = new BountyEventRow
        {
            Enabled = bounty.Enabled, MinIntervalSeconds = bounty.MinIntervalSeconds, MaxIntervalSeconds = bounty.MaxIntervalSeconds,
            DurationSeconds = bounty.DurationSeconds, RewardMass = bounty.RewardMass, FoodsCredit = bounty.FoodsCredit, MinPlayers = bounty.MinPlayers,
        };
        context.BountyEvents.Add(bountyRow);
        context.WorldEventRewards.AddRange(EventRewards(Bounty, bounty.RewardIds));
        context.MineFieldEvents.Add(new MineFieldEventRow
        {
            Enabled = mines.Enabled, MinIntervalSeconds = mines.MinIntervalSeconds, MaxIntervalSeconds = mines.MaxIntervalSeconds, MinMines = mines.MinMines,
            MaxMines = mines.MaxMines, Radius = mines.Radius, TriggerRadius = mines.TriggerRadius, ArmMilliseconds = mines.ArmMilliseconds, MinPlayers = mines.MinPlayers,
        });
        context.SugarRushEvents.Add(new SugarRushEventRow
        {
            Enabled = rush.Enabled, MinIntervalSeconds = rush.MinIntervalSeconds, MaxIntervalSeconds = rush.MaxIntervalSeconds,
            DurationSeconds = rush.DurationSeconds, MinPlayers = rush.MinPlayers,
        });
        context.WorldEventRooms.AddRange(EventRooms(FruitShower, shower.Rooms, shower.ExcludedRooms));
        context.WorldEventRooms.AddRange(EventRooms(Bounty, bounty.Rooms, bounty.ExcludedRooms));
        context.WorldEventRooms.AddRange(EventRooms(MineField, mines.Rooms, mines.ExcludedRooms));
        context.WorldEventRooms.AddRange(EventRooms(SugarRush, rush.Rooms, rush.ExcludedRooms));
        AddMaze(context, events.Maze);
    }

    private static void AddMaze(SnakeDbContext context, MazeOptions maze)
    {
        var row = new MazeEventRow
        {
            Enabled = maze.Enabled, MinIntervalSeconds = maze.MinIntervalSeconds, MaxIntervalSeconds = maze.MaxIntervalSeconds,
            DurationSeconds = maze.DurationSeconds, Columns = maze.Columns, Rows = maze.Rows, CorridorHeads = maze.CorridorHeads,
            Twistiness = maze.Twistiness, WallMassLoss = maze.WallMassLoss, FoodPercent = maze.FoodPercent, MinPlayers = maze.MinPlayers,
            PrizeMass = maze.PrizeMass, PrizeFoodsCredit = maze.PrizeFoodsCredit, PrizeGrantsPerk = NullIfBlank(maze.PrizeGrantsPerk),
            PrizeColor = maze.PrizeColor,
        };
        context.MazeEvents.Add(row);
        context.WorldEventRewards.AddRange(EventRewards(Maze, maze.RewardIds));
        context.WorldEventRooms.AddRange(EventRooms(Maze, maze.Rooms, maze.ExcludedRooms));
    }

    /// <summary>The config in the tables and the revision it was saved as, or null when nothing was ever saved.</summary>
    public static GameplayDocument? Read(SnakeDbContext context)
    {
        var settings = context.GameplaySettings.AsNoTracking().SingleOrDefault();
        if (settings is null)
            return null;
        var rooms = context.WorldEventRooms.AsNoTracking().ToList();
        var rewards = ReadRewards(context);
        var config = new GameplayConfig
        {
            Rewards = rewards.Catalog,
            Perks = ReadPerks(context, settings),
            Prize = ReadPrizes(context, settings, rewards.PrizeIds),
            Rooms = new RoomLifetimeOptions { EmptyPrivateRoomMinutes = settings.EmptyPrivateRoomMinutes, MaxPrivateRoomMinutes = settings.MaxPrivateRoomMinutes },
            WorldEvents = ReadEvents(context, settings, rooms, rewards.EventIds),
        }.WithRewardsResolved();
        return new GameplayDocument(settings.Revision, config);
    }

    private sealed record RewardRows(List<RewardDefinition> Catalog, ILookup<string, string> PrizeIds, ILookup<string, string> EventIds);

    private static RewardRows ReadRewards(SnakeDbContext context)
    {
        var catalog = context.Rewards.AsNoTracking().OrderBy(row => row.SortOrder).ToList().Select(row => new RewardDefinition
        {
            Id = row.Id, Name = row.Name,
            Reward = new PerkReward
            {
                GrantsRandomPerk = row.GrantsRandomPerk, ExtraLives = row.ExtraLives, TierSurgeMin = row.TierSurgeMin, TierSurgeMax = row.TierSurgeMax,
                TierSurgePerk = row.TierSurgePerk, BonusMass = row.BonusMass, Mend = row.Mend,
            },
        }).ToList();
        var prizeIds = context.PrizeRewards.AsNoTracking().OrderBy(row => row.Position).ToList().ToLookup(row => row.PrizeId, row => row.RewardId);
        var eventIds = context.WorldEventRewards.AsNoTracking().OrderBy(row => row.Position).ToList().ToLookup(row => row.Event, row => row.RewardId);
        return new RewardRows(catalog, prizeIds, eventIds);
    }

    public static string? ReadRevision(SnakeDbContext context) => context.GameplaySettings.AsNoTracking().Select(row => row.Revision).SingleOrDefault();

    private static PerkOptions ReadPerks(SnakeDbContext context, GameplaySettingsRow settings)
    {
        var effects = context.PerkEffects.AsNoTracking().OrderBy(row => row.Position).ToLookup(row => row.PerkId);
        var tiers = context.PerkTiers.AsNoTracking().OrderBy(row => row.Position).ToLookup(row => row.PerkId);
        var rooms = context.PerkRooms.AsNoTracking().OrderBy(row => row.Position).ToLookup(row => row.PerkId);
        var perks = context.Perks.AsNoTracking().OrderBy(row => row.SortOrder).ToList().Select(row => new PerkDefinition
        {
            Id = row.Id, Name = row.Name, FoodsRequired = row.FoodsRequired, Enabled = row.Enabled, Icon = row.Icon, Description = row.Description, Script = row.Script,
            CooldownSeconds = row.CooldownSeconds, DurationSeconds = row.DurationSeconds,
            Effects = effects[row.Id].Select(effect => new PerkEffectEntry { Effect = Enum.Parse<PerkEffect>(effect.Effect), Amount = effect.Amount, Target = effect.Target }).ToList(),
            Tiers = tiers[row.Id].Select(tier => new PerkTier { FoodsRequired = tier.FoodsRequired, Multiplier = tier.Multiplier, RewardOnly = tier.RewardOnly }).ToList(),
            Rooms = rooms[row.Id].Where(room => !room.IsExcluded).Select(room => room.Room).ToList(),
            ExcludedRooms = rooms[row.Id].Where(room => room.IsExcluded).Select(room => room.Room).ToList(),
        }).ToList();
        return new PerkOptions
        {
            Enabled = settings.PerksEnabled, ShotSpreadDegrees = settings.ShotSpreadDegrees, BurstIntervalMilliseconds = settings.BurstIntervalMilliseconds,
            KillFoodsCredit = settings.KillFoodsCredit, ExtraLifeKeepShare = settings.ExtraLifeKeepShare,
            ExtraLifeProtectSeconds = settings.ExtraLifeProtectSeconds, Perks = perks,
        };
    }

    private static PrizeOptions ReadPrizes(SnakeDbContext context, GameplaySettingsRow settings, ILookup<string, string> rewardIds)
    {
        var rooms = context.PrizeRooms.AsNoTracking().OrderBy(row => row.Position).ToLookup(row => row.PrizeId);
        var prizes = context.Prizes.AsNoTracking().OrderBy(row => row.SortOrder).ToList().Select(row => new PrizeDefinition
        {
            Id = row.Id, Name = row.Name, Enabled = row.Enabled, SpawnIntervalSeconds = row.SpawnIntervalSeconds, LifetimeSeconds = row.LifetimeSeconds,
            Value = row.Value, FoodsCredit = row.FoodsCredit, MinPlayers = row.MinPlayers, GrantsPerk = row.GrantsPerk, Color = row.Color,
            RewardIds = rewardIds[row.Id].ToList(),
            Rooms = rooms[row.Id].Where(room => !room.IsExcluded).Select(room => room.Room).ToList(),
            ExcludedRooms = rooms[row.Id].Where(room => room.IsExcluded).Select(room => room.Room).ToList(),
        }).ToList();
        return new PrizeOptions { Enabled = settings.PrizesEnabled, Prizes = prizes };
    }

    private static WorldEventOptions ReadEvents(SnakeDbContext context, GameplaySettingsRow settings, List<WorldEventRoomRow> rooms, ILookup<string, string> rewardIds)
    {
        var lightning = context.LightningEvents.AsNoTracking().Single();
        var meteors = context.MeteorEvents.AsNoTracking().Single();
        var zone = context.ZoneEvents.AsNoTracking().Single();
        var turret = context.Bosses.AsNoTracking().Single(row => row.Id == Turret);
        return new WorldEventOptions
        {
            Enabled = settings.WorldEventsEnabled,
            Lightning = new LightningOptions
            {
                Enabled = lightning.Enabled, MinIntervalSeconds = lightning.MinIntervalSeconds, MaxIntervalSeconds = lightning.MaxIntervalSeconds, Radius = lightning.Radius,
                TelegraphMilliseconds = lightning.TelegraphMilliseconds, MinStrikes = lightning.MinStrikes, MaxStrikes = lightning.MaxStrikes,
                StormSeconds = lightning.StormSeconds, AimedShare = lightning.AimedShare,
                Rooms = RoomsOf(rooms, Lightning, excluded: false), ExcludedRooms = RoomsOf(rooms, Lightning, excluded: true),
            },
            Meteors = new MeteorOptions
            {
                Enabled = meteors.Enabled, MinIntervalSeconds = meteors.MinIntervalSeconds, MaxIntervalSeconds = meteors.MaxIntervalSeconds, Radius = meteors.Radius,
                TelegraphMilliseconds = meteors.TelegraphMilliseconds, MinMeteors = meteors.MinMeteors, MaxMeteors = meteors.MaxMeteors, RainSeconds = meteors.RainSeconds,
                AimedShare = meteors.AimedShare, Rooms = RoomsOf(rooms, Meteors, excluded: false), ExcludedRooms = RoomsOf(rooms, Meteors, excluded: true),
            },
            Turret = new TurretOptions
            {
                Enabled = turret.Enabled, MinIntervalSeconds = turret.MinIntervalSeconds, MaxIntervalSeconds = turret.MaxIntervalSeconds, LifetimeSeconds = turret.LifetimeSeconds,
                HitPoints = turret.HitPoints, Size = turret.Size, FireIntervalMilliseconds = turret.FireIntervalMilliseconds, Arms = turret.Arms,
                SpinDegrees = turret.SpinDegrees, ShotSpeed = turret.ShotSpeed, MinPlayers = turret.MinPlayers, RewardMass = turret.RewardMass,
                GrantsPerk = turret.GrantsPerk, FoodsCredit = turret.FoodsCredit, RewardIds = rewardIds[Turret].ToList(),
                Rooms = RoomsOf(rooms, Turret, excluded: false), ExcludedRooms = RoomsOf(rooms, Turret, excluded: true),
            },
            Zone = new ZoneOptions
            {
                Enabled = zone.Enabled, MinIntervalSeconds = zone.MinIntervalSeconds, MaxIntervalSeconds = zone.MaxIntervalSeconds, WarningSeconds = zone.WarningSeconds,
                ShrinkSeconds = zone.ShrinkSeconds, HoldSeconds = zone.HoldSeconds, FinalRadius = zone.FinalRadius,
                DamageIntervalMilliseconds = zone.DamageIntervalMilliseconds, DamageMass = zone.DamageMass, MinPlayers = zone.MinPlayers,
                Rooms = RoomsOf(rooms, Zone, excluded: false), ExcludedRooms = RoomsOf(rooms, Zone, excluded: true),
            },
            FruitShower = ReadFruitShower(context, rooms),
            Bounty = ReadBounty(context, rooms, rewardIds[Bounty].ToList()),
            Mines = ReadMineField(context, rooms),
            SugarRush = ReadSugarRush(context, rooms),
            Maze = ReadMaze(context, rooms, rewardIds[Maze].ToList()),
        };
    }

    // The four below may have no row: a config saved before they existed. Then the defaults stand until the next save writes the row.

    private static FruitShowerOptions ReadFruitShower(SnakeDbContext context, List<WorldEventRoomRow> rooms)
    {
        var row = context.FruitShowerEvents.AsNoTracking().SingleOrDefault();
        if (row is null)
            return new FruitShowerOptions();
        return new FruitShowerOptions
        {
            Enabled = row.Enabled, MinIntervalSeconds = row.MinIntervalSeconds, MaxIntervalSeconds = row.MaxIntervalSeconds, DurationSeconds = row.DurationSeconds,
            FruitsPerSecond = row.FruitsPerSecond, MinPlayers = row.MinPlayers,
            Rooms = RoomsOf(rooms, FruitShower, excluded: false), ExcludedRooms = RoomsOf(rooms, FruitShower, excluded: true),
        };
    }

    private static BountyOptions ReadBounty(SnakeDbContext context, List<WorldEventRoomRow> rooms, List<string> rewardIds)
    {
        var row = context.BountyEvents.AsNoTracking().SingleOrDefault();
        if (row is null)
            return new BountyOptions();
        return new BountyOptions
        {
            Enabled = row.Enabled, MinIntervalSeconds = row.MinIntervalSeconds, MaxIntervalSeconds = row.MaxIntervalSeconds, DurationSeconds = row.DurationSeconds,
            RewardMass = row.RewardMass, FoodsCredit = row.FoodsCredit, MinPlayers = row.MinPlayers, RewardIds = rewardIds,
            Rooms = RoomsOf(rooms, Bounty, excluded: false), ExcludedRooms = RoomsOf(rooms, Bounty, excluded: true),
        };
    }

    private static MineFieldOptions ReadMineField(SnakeDbContext context, List<WorldEventRoomRow> rooms)
    {
        var row = context.MineFieldEvents.AsNoTracking().SingleOrDefault();
        if (row is null)
            return new MineFieldOptions();
        return new MineFieldOptions
        {
            Enabled = row.Enabled, MinIntervalSeconds = row.MinIntervalSeconds, MaxIntervalSeconds = row.MaxIntervalSeconds, MinMines = row.MinMines,
            MaxMines = row.MaxMines, Radius = row.Radius, TriggerRadius = row.TriggerRadius, ArmMilliseconds = row.ArmMilliseconds, MinPlayers = row.MinPlayers,
            Rooms = RoomsOf(rooms, MineField, excluded: false), ExcludedRooms = RoomsOf(rooms, MineField, excluded: true),
        };
    }

    private static MazeOptions ReadMaze(SnakeDbContext context, List<WorldEventRoomRow> rooms, List<string> rewardIds)
    {
        var row = context.MazeEvents.AsNoTracking().SingleOrDefault();
        if (row is null)
            return new MazeOptions();
        return new MazeOptions
        {
            Enabled = row.Enabled, MinIntervalSeconds = row.MinIntervalSeconds, MaxIntervalSeconds = row.MaxIntervalSeconds, DurationSeconds = row.DurationSeconds,
            Columns = row.Columns, Rows = row.Rows, CorridorHeads = row.CorridorHeads, Twistiness = row.Twistiness, WallMassLoss = row.WallMassLoss,
            FoodPercent = row.FoodPercent, MinPlayers = row.MinPlayers, PrizeMass = row.PrizeMass, PrizeFoodsCredit = row.PrizeFoodsCredit,
            PrizeGrantsPerk = row.PrizeGrantsPerk, PrizeColor = row.PrizeColor, RewardIds = rewardIds,
            Rooms = RoomsOf(rooms, Maze, excluded: false), ExcludedRooms = RoomsOf(rooms, Maze, excluded: true),
        };
    }

    private static SugarRushOptions ReadSugarRush(SnakeDbContext context, List<WorldEventRoomRow> rooms)
    {
        var row = context.SugarRushEvents.AsNoTracking().SingleOrDefault();
        if (row is null)
            return new SugarRushOptions();
        return new SugarRushOptions
        {
            Enabled = row.Enabled, MinIntervalSeconds = row.MinIntervalSeconds, MaxIntervalSeconds = row.MaxIntervalSeconds, DurationSeconds = row.DurationSeconds,
            MinPlayers = row.MinPlayers,
            Rooms = RoomsOf(rooms, SugarRush, excluded: false), ExcludedRooms = RoomsOf(rooms, SugarRush, excluded: true),
        };
    }

    private static List<string> RoomsOf(List<WorldEventRoomRow> rows, string kind, bool excluded)
        => rows.Where(row => row.Event == kind && row.IsExcluded == excluded).OrderBy(row => row.Position).Select(row => row.Room).ToList();

    private static IEnumerable<WorldEventRoomRow> EventRooms(string kind, List<string> allowed, List<string> excluded)
        => Rooms(allowed, excluded, (room, isExcluded, position) => new WorldEventRoomRow { Event = kind, IsExcluded = isExcluded, Position = position, Room = room });

    private static IEnumerable<TRow> Rooms<TRow>(List<string> allowed, List<string> excluded, Func<string, bool, int, TRow> make)
        => allowed.Select((room, position) => make(room, false, position)).Concat(excluded.Select((room, position) => make(room, true, position)));

    private static IEnumerable<WorldEventRewardRow> EventRewards(string kind, List<string> rewardIds)
        => rewardIds.Select((rewardId, position) => new WorldEventRewardRow { Event = kind, Position = position, RewardId = rewardId });

    private static string? NullIfBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;
}
