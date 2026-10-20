using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Library.Commons.Game.Server.Entities.Hub.Events;

namespace Application.Game.Server.Services;

/// <summary>
/// Turns the state of a room into what is sent to its players. The game entities are not sent as they are: they carry
/// everything the server needs and several times what a screen needs, and a snapshot goes to every player 30 times a second.
/// </summary>
public static class SnapshotBuilder
{
    /// <param name="now">The moment the snapshot is for; lightning warnings count down to it. Defaults to the clock.</param>
    public static SnapshotEvent Build(IGameState gameState, DateTimeOffset? now = null)
    {
        ArgumentNullException.ThrowIfNull(gameState);
        var at = now ?? DateTimeOffset.UtcNow;
        return new SnapshotEvent
        {
            Room = new SnapshotRoom { Guid = gameState.Room.Guid, Width = gameState.Room.Width, Height = gameState.Room.Height },
            Snakes = gameState.Players.Values.Where(player => player.Path.Count > 0).Select(ToSnake).ToList(),
            Foods = gameState.Foods.Values.Select(ToFood).ToList(),
            Shots = gameState.Obstacles.Values.Select(ToShot).ToList(),
            Strikes = ToStrikes(gameState, now ?? DateTimeOffset.UtcNow),
            Boss = ToBoss(gameState.Boss),
            Zone = ToZone(gameState.Zone, now ?? DateTimeOffset.UtcNow),
            Mines = ToMines(gameState, at),
            Bounty = ToBounty(gameState.Bounty, at),
            Shower = MillisecondsLeft(gameState.FruitShowerEndsAt, at),
            Rush = MillisecondsLeft(gameState.SugarRushEndsAt, at),
            Maze = ToMaze(gameState.Maze, at),
        };
    }

    private static int? MillisecondsLeft(DateTimeOffset? endsAt, DateTimeOffset now)
        => endsAt is { } end ? (int)Math.Max(0, Math.Round((end - now).TotalMilliseconds)) : null;

    private static SnapshotBounty? ToBounty(WorldBounty? bounty, DateTimeOffset now)
        => bounty is null
            ? null
            : new SnapshotBounty { Guid = bounty.Target, T = (int)Math.Max(0, Math.Round((bounty.EndsAt - now).TotalMilliseconds)), M = bounty.RewardMass };

    private static SnapshotMaze? ToMaze(WorldMaze? maze, DateTimeOffset now)
        => maze is null
            ? null
            : new SnapshotMaze
            {
                X = (int)Math.Round(maze.X),
                Y = (int)Math.Round(maze.Y),
                C = maze.Columns,
                R = maze.Rows,
                K = maze.Corridor,
                V = maze.Twistiness,
                S = maze.Seed,
                U = (int)Math.Round(maze.Unit),
                T = (int)Math.Max(0, Math.Round((maze.EndsAt - now).TotalMilliseconds)),
            };

    private static List<SnapshotMine>? ToMines(IGameState gameState, DateTimeOffset now)
    {
        if (gameState.Mines is not { IsEmpty: false }) return null;
        return gameState.Mines.Values.Select(mine => new SnapshotMine
        {
            X = (int)Math.Round(mine.X),
            Y = (int)Math.Round(mine.Y),
            R = (int)Math.Round(mine.Radius),
            T = (int)Math.Max(0, Math.Round((mine.ArmedAt - now).TotalMilliseconds)),
        }).ToList();
    }

    private static SnapshotZone? ToZone(WorldZone? zone, DateTimeOffset now)
        => zone is null
            ? null
            : new SnapshotZone
            {
                X = (int)Math.Round(zone.X),
                Y = (int)Math.Round(zone.Y),
                R = (int)Math.Round(zone.Radius),
                F = (int)Math.Round(zone.FinalRadius),
                P = (int)zone.Phase,
                T = (int)Math.Max(0, Math.Round((zone.PhaseEndsAt - now).TotalMilliseconds)),
            };

    private static SnapshotBoss? ToBoss(WorldBoss? boss)
    {
        if (boss is null) return null;
        var degrees = (int)Math.Round(boss.Angle * 180 / Math.PI) % 360;
        return new SnapshotBoss
        {
            Guid = boss.Id,
            X = (int)Math.Round(boss.X),
            Y = (int)Math.Round(boss.Y),
            S = (int)Math.Round(boss.Size),
            Hp = (int)Math.Clamp(Math.Round(boss.Hp / Math.Max(boss.MaxHp, 1) * 100), 1, 100),
            A = degrees < 0 ? degrees + 360 : degrees,
            N = boss.Arms,
        };
    }

    private static List<SnapshotStrike>? ToStrikes(IGameState gameState, DateTimeOffset now)
    {
        if (gameState.Strikes is not { IsEmpty: false }) return null;
        return gameState.Strikes.Values.Select(strike => new SnapshotStrike
        {
            X = (int)Math.Round(strike.X),
            Y = (int)Math.Round(strike.Y),
            R = (int)Math.Round(strike.Radius),
            T = (int)Math.Max(0, Math.Round((strike.StrikesAt - now).TotalMilliseconds)),
            K = (int)strike.Kind,
        }).ToList();
    }

    private static SnapshotSnake ToSnake(IPlayer player) => new()
    {
        Guid = player.Guid,
        Id = player.Id,
        Name = player.Name,
        Dead = !player.IsAlive,
        Boost = player.IsBoosting,
        AutoFire = player.AutoFire,
        Score = player.Score,
        FoodsEaten = player.FoodsEaten,
        Color = player.BackgroundColor.FillColor,
        Direction = DirectionName(player.CurrentDirection),
        Perks = player.Perks.Count == 0 ? null : player.Perks.ToList(),
        PerkTiers = player.PerkTiers.Any(tier => tier > 1) ? player.PerkTiers.ToList() : null,
        Path = player.Path.SelectMany(point => new[] { Tenth(point.X), Tenth(point.Y) }).ToList(),
        Health = HealthPercents(player.SegmentHealth),
    };

    private static List<int>? HealthPercents(IList<double> ratios)
    {
        var percents = ratios.Select(ratio => (int)Math.Clamp(Math.Round(ratio * 100), 0, 100)).ToList();
        return percents.All(percent => percent == 100) ? null : percents;
    }

    private static SnapshotFood ToFood(IFood food) => new()
    {
        X = (int)Math.Round(food.Position.X),
        Y = (int)Math.Round(food.Position.Y),
        Value = food.Value == 1 ? null : food.Value,
        Kind = food.Kind == FoodKind.Prize ? 1 : null,
        Color = food.Color.FillColor,
    };

    private static SnapshotShot ToShot(IObstacle shot) => new()
    {
        Guid = shot.Guid,
        X = Tenth(shot.CurrentPosition.X),
        Y = Tenth(shot.CurrentPosition.Y),
        Size = shot.Size,
        Owner = shot.CreatedyBy.Guid,
    };

    private static double Tenth(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    /// <summary>The names the client knows: left, up, right, down, angular.</summary>
    private static string DirectionName(Direction direction) => char.ToLowerInvariant(direction.ToString()[0]) + direction.ToString()[1..];
}
