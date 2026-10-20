import { describe, expect, it } from "vitest";
import { generateMaze } from "@/game/maze";
import type { GameSnapshot, MazeSnapshot, SnakeSnapshot } from "@/types/game";
import { mazeEntranceCenter, mazeLayoutOf, mazePrizeCenter, mazeWarning, visibleBlocks, wallBlocks } from "./maze";

const maze: MazeSnapshot = { position: { x: 300, y: 200 }, columns: 6, rows: 5, corridor: 3, twistiness: 50, seed: 12345, unit: 15, msLeft: 60000 };
const world = { width: 1920, height: 1080 };

const snapshotWith = (parts: Partial<GameSnapshot> = {}): GameSnapshot =>
  ({ room: { guid: null, ...world }, snakes: [], foods: [], shots: [], strikes: [], boss: null, zone: null, ...parts }) as GameSnapshot;

const snake = (x: number, y: number, isAlive = true): SnakeSnapshot => ({ guid: "me", head: { x, y }, isAlive }) as SnakeSnapshot;

describe("mazeLayoutOf", () => {
  it("builds the walls the generator builds from the same numbers", () => {
    expect(mazeLayoutOf(maze).toAscii()).toEqual(generateMaze(12345, 6, 5, 50, 3).toAscii());
  });

  it("keeps the layout while the same maze stands, and builds another for another seed", () => {
    expect(mazeLayoutOf({ ...maze, msLeft: 1 })).toBe(mazeLayoutOf(maze));
    expect(mazeLayoutOf({ ...maze, seed: 99 })).not.toBe(mazeLayoutOf(maze));
  });
});

describe("visibleBlocks", () => {
  const layout = mazeLayoutOf(maze);

  it("is every block when the clip holds the maze", () => {
    const ranges = visibleBlocks(maze, layout, { left: 0, top: 0, right: 1920, bottom: 1080 }, world);

    expect(ranges).toEqual([{ firstColumn: 0, lastColumn: layout.blockColumns - 1, firstRow: 0, lastRow: layout.blockRows - 1 }]);
  });

  it("is only the blocks under a small clip", () => {
    const ranges = visibleBlocks(maze, layout, { left: 300, top: 200, right: 329, bottom: 214 }, world);

    expect(ranges).toEqual([{ firstColumn: 0, lastColumn: 1, firstRow: 0, lastRow: 0 }]);
  });

  it("is nothing when the clip is somewhere else", () => {
    expect(visibleBlocks(maze, layout, { left: 1000, top: 800, right: 1500, bottom: 1000 }, world)).toEqual([]);
  });

  it("finds the maze through the wrap: a clip given one room to the right", () => {
    const ranges = visibleBlocks(maze, layout, { left: 300 + 1920, top: 200, right: 329 + 1920, bottom: 214 }, world);

    expect(ranges).toEqual([{ firstColumn: 0, lastColumn: 1, firstRow: 0, lastRow: 0 }]);
  });
});

describe("wallBlocks", () => {
  const layout = mazeLayoutOf(maze);

  it("lists exactly the wall blocks, at their world positions", () => {
    const ranges = visibleBlocks(maze, layout, { left: 0, top: 0, right: 1920, bottom: 1080 }, world);
    const blocks = wallBlocks(maze, layout, ranges);

    const expected = layout
      .toAscii()
      .join("")
      .split("")
      .filter((cell) => cell === "#").length;
    expect(blocks).toHaveLength(expected);
    expect(blocks[0]).toMatchObject({ column: 0, row: 0, position: { x: 300, y: 200 } });
    for (const block of blocks) expect(layout.isWall(block.column, block.row)).toBe(true);
  });

  it("says where the light falls: the top of the outer wall is lit, the bottom of a wall above floor is shaded", () => {
    const blocks = wallBlocks(maze, layout, [{ firstColumn: 0, lastColumn: 0, firstRow: 0, lastRow: 0 }]);

    expect(blocks).toEqual([{ column: 0, row: 0, position: { x: 300, y: 200 }, lit: true, shaded: false }]);
  });
});

describe("the prize and the entrance", () => {
  const layout = mazeLayoutOf(maze);

  it("the prize is the middle of the far cell", () => {
    const { column, row } = layout.cellCenter(layout.prizeCell);

    expect(mazePrizeCenter(maze)).toEqual({ x: 300 + column * 15, y: 200 + row * 15 });
  });

  it("the entrance is on the outer wall of the side it is on", () => {
    const entrance = mazeEntranceCenter(maze);
    const width = layout.blockColumns * 15;
    const height = layout.blockRows * 15;
    const onEdge = [300, 300 + width - 15].includes(entrance.x) || [200, 200 + height - 15].includes(entrance.y);

    expect(layout.entranceSide).toBe("South");
    expect(entrance.y).toBe(200 + (layout.blockRows - 1) * 15);
    expect(onEdge).toBe(true);
  });
});

describe("mazeWarning", () => {
  it("is null while there is no maze", () => {
    expect(mazeWarning(snapshotWith(), "me")).toBeNull();
    expect(mazeWarning(snapshotWith({ maze: null }), "me")).toBeNull();
  });

  it("tells the time left and whether the own snake is inside", () => {
    const inside = snapshotWith({ maze, snakes: [snake(400, 300)] });
    const outside = snapshotWith({ maze, snakes: [snake(100, 100)] });

    expect(mazeWarning(inside, "me")).toEqual({ msLeft: 60000, inside: true });
    expect(mazeWarning(outside, "me")).toEqual({ msLeft: 60000, inside: false });
  });

  it("a dead snake, or none of ours, is outside", () => {
    expect(mazeWarning(snapshotWith({ maze, snakes: [snake(400, 300, false)] }), "me")!.inside).toBe(false);
    expect(mazeWarning(snapshotWith({ maze, snakes: [snake(400, 300)] }), "someone else")!.inside).toBe(false);
  });
});
