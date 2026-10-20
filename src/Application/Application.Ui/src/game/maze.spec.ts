import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { describe, expect, it } from "vitest";
import { generateMaze } from "./maze";

interface VectorCase {
  seed: number;
  columns: number;
  rows: number;
  twistiness: number;
  corridor: number;
  entranceSide: string;
  entranceCell: number;
  prizeCell: number;
  walls: string[];
}

// The same file Domain.Game.Tests checks the C# generator against: the two must build the same walls.
const vector: VectorCase[] = JSON.parse(
  readFileSync(fileURLToPath(new URL("../../../../Tests/Shared/maze-vector.json", import.meta.url)), "utf8"),
);

describe("generateMaze", () => {
  it("has the shared vector to check against", () => {
    expect(vector.length).toBeGreaterThanOrEqual(5);
  });

  it.each(vector.map((c) => [`seed ${c.seed} ${c.columns}x${c.rows} twist ${c.twistiness} corridor ${c.corridor}`, c] as const))(
    "builds the same walls as the C# generator: %s",
    (_name, c) => {
      const maze = generateMaze(c.seed, c.columns, c.rows, c.twistiness, c.corridor);

      expect(maze.toAscii()).toEqual(c.walls);
      expect(maze.entranceSide).toBe(c.entranceSide);
      expect(maze.entranceCell).toBe(c.entranceCell);
      expect(maze.prizeCell).toBe(c.prizeCell);
    },
  );

  it("gives the same maze for the same seed", () => {
    expect(generateMaze(9, 7, 6, 40, 3).toAscii()).toEqual(generateMaze(9, 7, 6, 40, 3).toAscii());
  });

  it("treats everything outside the grid as open", () => {
    const maze = generateMaze(9, 7, 6, 40, 3);

    expect(maze.isWall(-1, 0)).toBe(false);
    expect(maze.isWall(maze.blockColumns, 0)).toBe(false);
    expect(maze.isWall(0, maze.blockRows)).toBe(false);
  });

  it("refuses parameters out of range", () => {
    expect(() => generateMaze(1, 2, 5, 50, 3)).toThrow(RangeError);
    expect(() => generateMaze(1, 5, 5, 101, 3)).toThrow(RangeError);
    expect(() => generateMaze(1, 5, 5, 50, 2)).toThrow(RangeError);
  });
});
