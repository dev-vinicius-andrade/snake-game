using Domain.Game.Entities;
using Domain.Game.Helpers;
using Domain.Game.Rules;
using Library.Commons.Game.Domain.Entities;
using Library.Commons.Game.Domain.Entities.Configurations;
using Library.Commons.Game.Domain.Enums;
using Library.Commons.Game.Domain.Interfaces.Entities;
using Microsoft.Extensions.Options;

namespace Domain.Game.Services;

/// <summary>
/// A maze: every now and then a procedurally generated maze rises in a clear part of a room for a while, with a big prize at its far
/// end. The walls stop heads (at a cost of mass, see <see cref="IGameStateService"/>'s simulation) and shots. The maze goes when its time
/// is up or the prize is taken, and its prize goes with it if it is still there. Fruit inside is left where it lies.
/// Sizes are in head sizes; the walls are one head size thick.
/// </summary>
public sealed class MazeRunner(WorldEventRules rules, EventSchedule schedule, IOptions<SnakeConfiguration> snake, TimeProvider time)
    : ScheduledEventRunner(rules, schedule, time)
{
    /// <summary>Free space left between the maze and the edges of the room, in head sizes, so snakes can go round it.</summary>
    private const int MarginHeads = 4;

    /// <summary>A maze does not rise this close to a living head, in head sizes.</summary>
    private const int ClearanceHeads = 2;

    private const int PlacementAttempts = 30;

    protected override IScheduledEvent SettingsOf(WorldEventOptions options) => options.Maze;

    protected override int DurationSeconds(WorldEventOptions options) => options.Maze.DurationSeconds;

    protected override void Begin(IGameState gameState, WorldEventOptions options, DateTimeOffset now)
    {
        var maze = options.Maze;
        var unit = snake.Value.HeadSize;
        var room = gameState.Room;
        var pitch = (maze.CorridorHeads + 1) * unit;
        var margin = MarginHeads * unit;
        var columns = Math.Min(maze.Columns, (room.Width - 2 * margin - unit) / pitch);
        var rows = Math.Min(maze.Rows, (room.Height - 2 * margin - unit) / pitch);
        if (columns < MazeGenerator.MinCells || rows < MazeGenerator.MinCells) return;

        var spanWidth = columns * pitch + unit;
        var spanHeight = rows * pitch + unit;
        if (PlaceAwayFromHeads(gameState, unit, spanWidth, spanHeight, margin) is not var (x, y)) return;

        var seed = (uint)Math.Min(Schedule.Next() * 4294967296.0, 4294967295.0);
        var layout = MazeGenerator.Generate(seed, columns, rows, maze.Twistiness, maze.CorridorHeads);
        var prize = AddPrize(gameState, maze, layout, x, y, unit);
        gameState.Maze = new WorldMaze(x, y, columns, rows, maze.CorridorHeads, maze.Twistiness, seed, unit,
            now.AddSeconds(maze.DurationSeconds), layout, prize.Guid, maze.WallMassLoss);
        AddFruits(gameState, maze, layout, x, y, unit);
    }

    /// <summary>A spot for the maze that keeps off the heads of living snakes, or null when none of a few tries is clear.</summary>
    private (double X, double Y)? PlaceAwayFromHeads(IGameState gameState, int unit, int width, int height, int margin)
    {
        var room = gameState.Room;
        var heads = gameState.Players.Values.Where(player => player.IsAlive).Select(player => player.CurrentPosition).ToArray();
        var clearance = ClearanceHeads * unit;
        for (var attempt = 0; attempt < PlacementAttempts; attempt++)
        {
            var x = Math.Floor(margin + Schedule.Next() * (room.Width - 2 * margin - width));
            var y = Math.Floor(margin + Schedule.Next() * (room.Height - 2 * margin - height));
            var covered = heads.Any(head =>
                head.X + unit > x - clearance && head.X < x + width + clearance && head.Y + unit > y - clearance && head.Y < y + height + clearance);
            if (!covered) return (x, y);
        }
        return null;
    }

    private IFood AddPrize(IGameState gameState, MazeOptions maze, MazeLayout layout, double x, double y, int unit)
    {
        var prize = new Food(Guid.NewGuid())
        {
            Position = CenterOf(layout, layout.PrizeCell, x, y, unit),
            Color = new Color(maze.PrizeColor, maze.PrizeColor),
            Value = maze.PrizeMass,
            Kind = FoodKind.Prize,
            FoodsCredit = maze.PrizeFoodsCredit,
            PrizeId = MazeOptions.PrizeId,
            GrantsPerk = string.IsNullOrWhiteSpace(maze.PrizeGrantsPerk) ? null : maze.PrizeGrantsPerk,
        };
        gameState.Foods.Add(prize.Guid, prize);
        return prize;
    }

    private void AddFruits(IGameState gameState, MazeOptions maze, MazeLayout layout, double x, double y, int unit)
    {
        if (maze.FoodPercent <= 0) return;
        for (var cell = 0; cell < layout.Columns * layout.Rows; cell++)
        {
            if (cell == layout.EntranceCell || cell == layout.PrizeCell || (int)(Schedule.Next() * 100) >= maze.FoodPercent) continue;
            var color = RandomHelper.RandomColor();
            var fruit = new Food(Guid.NewGuid()) { Position = CenterOf(layout, cell, x, y, unit), Color = new Color(color, color) };
            gameState.Foods.Add(fruit.Guid, fruit);
        }
    }

    /// <summary>The top left corner of a head sized square that sits in the middle of the cell.</summary>
    private static PointAxis CenterOf(MazeLayout layout, int cell, double x, double y, int unit)
    {
        var (column, row) = layout.CellCenter(cell);
        return new PointAxis { X = Math.Round(x + column * unit - unit / 2.0), Y = Math.Round(y + row * unit - unit / 2.0), Z = 0 };
    }

    /// <summary>The maze is over when the prize has been taken (or is gone some other way), or it never rose.</summary>
    protected override bool IsOver(IGameState gameState)
        => gameState.Maze is not { } maze || !gameState.Foods.ContainsKey(maze.PrizeId);

    protected override void Clear(IGameState gameState)
    {
        if (gameState.Maze is { } maze) gameState.Foods.Remove(maze.PrizeId);
        gameState.Maze = null;
    }
}
