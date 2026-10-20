import { generateMaze, type MazeLayout } from "@/game/maze";
import type { GamePoint, GameSnapshot, MazeSnapshot, RoomDimensions } from "@/types/game";

/** A part of the world, in world units. */
export interface WorldClip {
  left: number;
  top: number;
  right: number;
  bottom: number;
}

/** Columns and rows of blocks, both ends included. */
export interface BlockRange {
  firstColumn: number;
  lastColumn: number;
  firstRow: number;
  lastRow: number;
}

let cached: { key: string; layout: MazeLayout } | null = null;

const keyOf = (maze: MazeSnapshot) => `${maze.seed}/${maze.columns}/${maze.rows}/${maze.twistiness}/${maze.corridor}`;

/** The walls of the maze, built once from the seed and kept while the same maze stands (the snapshot comes thirty times a second). */
export function mazeLayoutOf(maze: MazeSnapshot): MazeLayout {
  const key = keyOf(maze);
  if (cached?.key !== key) cached = { key, layout: generateMaze(maze.seed, maze.columns, maze.rows, maze.twistiness, maze.corridor) };
  return cached.layout;
}

/**
 * The blocks of the maze that a part of the world shows. The world wraps, so the part may be given in coordinates one room off
 * (a camera at the left edge sees the world from below zero); the maze never touches an edge, so each copy of the room is tried.
 */
export function visibleBlocks(maze: MazeSnapshot, layout: MazeLayout, clip: WorldClip, world: RoomDimensions): BlockRange[] {
  const ranges: BlockRange[] = [];
  for (const shiftX of [-world.width, 0, world.width])
    for (const shiftY of [-world.height, 0, world.height]) {
      const firstColumn = Math.max(0, Math.floor((clip.left + shiftX - maze.position.x) / maze.unit));
      const lastColumn = Math.min(layout.blockColumns - 1, Math.floor((clip.right + shiftX - maze.position.x) / maze.unit));
      const firstRow = Math.max(0, Math.floor((clip.top + shiftY - maze.position.y) / maze.unit));
      const lastRow = Math.min(layout.blockRows - 1, Math.floor((clip.bottom + shiftY - maze.position.y) / maze.unit));
      if (firstColumn <= lastColumn && firstRow <= lastRow) ranges.push({ firstColumn, lastColumn, firstRow, lastRow });
    }
  return ranges;
}

/** A wall block to draw: its place in the grid and the top left corner in world units. */
export interface WallBlock {
  column: number;
  row: number;
  position: GamePoint;
  /** Whether the block above it is open floor, which is where the light falls. */
  lit: boolean;
  /** Whether the block below it is open floor. */
  shaded: boolean;
}

/** The wall blocks in the ranges. */
export function wallBlocks(maze: MazeSnapshot, layout: MazeLayout, ranges: BlockRange[]): WallBlock[] {
  const blocks: WallBlock[] = [];
  for (const range of ranges)
    for (let row = range.firstRow; row <= range.lastRow; row++)
      for (let column = range.firstColumn; column <= range.lastColumn; column++) {
        if (!layout.isWall(column, row)) continue;
        blocks.push({
          column,
          row,
          position: { x: maze.position.x + column * maze.unit, y: maze.position.y + row * maze.unit },
          lit: !layout.isWall(column, row - 1),
          shaded: !layout.isWall(column, row + 1),
        });
      }
  return blocks;
}

/** The middle of the prize at the far end of the maze, in world units: where the compass points while the maze stands. */
export function mazePrizeCenter(maze: MazeSnapshot): GamePoint {
  const layout = mazeLayoutOf(maze);
  const { column, row } = layout.cellCenter(layout.prizeCell);
  return { x: maze.position.x + column * maze.unit, y: maze.position.y + row * maze.unit };
}

/** The middle of the opening of the outer wall, in world units: where to go in. */
export function mazeEntranceCenter(maze: MazeSnapshot): GamePoint {
  const layout = mazeLayoutOf(maze);
  const { column, row } = layout.cellCenter(layout.entranceCell);
  const pitch = layout.corridor + 1;
  const x = maze.position.x + column * maze.unit;
  const y = maze.position.y + row * maze.unit;
  switch (layout.entranceSide) {
    case "North":
      return { x, y: maze.position.y };
    case "South":
      return { x, y: maze.position.y + layout.rows * pitch * maze.unit };
    case "West":
      return { x: maze.position.x, y };
    default:
      return { x: maze.position.x + layout.columns * pitch * maze.unit, y };
  }
}

/** What the player is told while a maze stands: the time left, and whether the own snake is inside it. */
export interface MazeWarning {
  msLeft: number;
  inside: boolean;
}

/** Null while there is no maze. */
export function mazeWarning(snapshot: GameSnapshot, selfGuid: string | null | undefined): MazeWarning | null {
  const maze = snapshot.maze;
  if (!maze) return null;
  const own = snapshot.snakes.find((snake) => snake.guid === selfGuid && snake.isAlive);
  const layout = mazeLayoutOf(maze);
  const inside =
    !!own &&
    own.head.x >= maze.position.x &&
    own.head.x < maze.position.x + layout.blockColumns * maze.unit &&
    own.head.y >= maze.position.y &&
    own.head.y < maze.position.y + layout.blockRows * maze.unit;
  return { msLeft: maze.msLeft, inside };
}
