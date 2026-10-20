/**
 * The maze, built from a seed. A port of `Domain.Game/Rules/MazeGenerator.cs`: the server and the client must build the same walls,
 * so this follows it step by step, in integers only. Both are checked against `src/Tests/Shared/maze-vector.json`.
 *
 * The walls are a grid of square blocks. A wall is one block thick, a corridor `corridor` blocks wide, and a cell and the wall after it
 * span `corridor + 1` blocks. The corner blocks of every wall line are always wall.
 */

export type MazeSide = "North" | "East" | "South" | "West";

export const MIN_CELLS = 3;
export const MAX_CELLS = 30;
export const MIN_CORRIDOR = 3;
export const MAX_CORRIDOR = 12;
/** Walls opened after the carving, per hundred cells. */
export const LOOPS_PER_HUNDRED_CELLS = 6;

export interface MazeLayout {
  columns: number;
  rows: number;
  corridor: number;
  entranceSide: MazeSide;
  entranceCell: number;
  prizeCell: number;
  blockColumns: number;
  blockRows: number;
  /** Whether the block is wall. Outside the grid is open. */
  isWall(column: number, row: number): boolean;
  /** The middle of a cell, in blocks. */
  cellCenter(cell: number): { column: number; row: number };
  /** The walls as text, one line per row of blocks (`#` wall, `.` floor). */
  toAscii(): string[];
}

/** mulberry32: the same numbers from the same seed anywhere. */
class MazeRandom {
  private state: number;

  constructor(seed: number) {
    this.state = seed >>> 0;
  }

  next(): number {
    this.state = (this.state + 0x6d2b79f5) >>> 0;
    let t = this.state;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return (t ^ (t >>> 14)) >>> 0;
  }

  below(limit: number): number {
    return this.next() % limit;
  }
}

// North, east, south, west: the order is part of the vector.
const STEP_COLUMN = [0, 1, 0, -1];
const STEP_ROW = [-1, 0, 1, 0];
const SIDES: MazeSide[] = ["North", "East", "South", "West"];

export function generateMaze(seed: number, columns: number, rows: number, twistiness: number, corridor: number): MazeLayout {
  if (columns < MIN_CELLS || columns > MAX_CELLS) throw new RangeError(`columns must be ${MIN_CELLS} to ${MAX_CELLS}`);
  if (rows < MIN_CELLS || rows > MAX_CELLS) throw new RangeError(`rows must be ${MIN_CELLS} to ${MAX_CELLS}`);
  if (twistiness < 0 || twistiness > 100) throw new RangeError("twistiness must be 0 to 100");
  if (corridor < MIN_CORRIDOR || corridor > MAX_CORRIDOR) throw new RangeError(`corridor must be ${MIN_CORRIDOR} to ${MAX_CORRIDOR}`);

  const random = new MazeRandom(seed);
  const cells = columns * rows;
  const openEast = new Array<boolean>(cells).fill(false);
  const openSouth = new Array<boolean>(cells).fill(false);
  carve(random, columns, rows, twistiness, openEast, openSouth);
  openLoops(random, columns, rows, openEast, openSouth);
  const entranceSide = SIDES[random.below(4)];
  const entranceCell = pickEntranceCell(random, entranceSide, columns, rows);
  const prizeCell = farthest(columns, rows, entranceCell, openEast, openSouth);
  return layout(columns, rows, corridor, openEast, openSouth, entranceSide, entranceCell, prizeCell);
}

function carve(random: MazeRandom, columns: number, rows: number, twistiness: number, openEast: boolean[], openSouth: boolean[]): void {
  const cells = columns * rows;
  const visited = new Array<boolean>(cells).fill(false);
  const stack = new Array<number>(cells).fill(0);
  const arrivedBy = new Array<number>(cells).fill(0);
  const start = random.below(cells);
  visited[start] = true;
  stack[0] = start;
  arrivedBy[0] = -1;
  let depth = 1;
  const options: number[] = [];
  while (depth > 0) {
    const cell = stack[depth - 1];
    options.length = 0;
    for (let direction = 0; direction < 4; direction++) if (canVisit(cell, direction, columns, rows, visited)) options.push(direction);
    if (options.length === 0) {
      depth--;
      continue;
    }

    const came = arrivedBy[depth - 1];
    let chosen = -1;
    if (came >= 0 && options.includes(came) && random.below(100) >= twistiness) chosen = came;
    if (chosen < 0) chosen = options[random.below(options.length)];

    const next = (Math.floor(cell / columns) + STEP_ROW[chosen]) * columns + (cell % columns) + STEP_COLUMN[chosen];
    open(cell, chosen, columns, openEast, openSouth);
    visited[next] = true;
    stack[depth] = next;
    arrivedBy[depth] = chosen;
    depth++;
  }
}

function canVisit(cell: number, direction: number, columns: number, rows: number, visited: boolean[]): boolean {
  const column = (cell % columns) + STEP_COLUMN[direction];
  const row = Math.floor(cell / columns) + STEP_ROW[direction];
  return column >= 0 && row >= 0 && column < columns && row < rows && !visited[row * columns + column];
}

function open(cell: number, direction: number, columns: number, openEast: boolean[], openSouth: boolean[]): void {
  switch (direction) {
    case 0:
      openSouth[cell - columns] = true;
      break;
    case 1:
      openEast[cell] = true;
      break;
    case 2:
      openSouth[cell] = true;
      break;
    default:
      openEast[cell - 1] = true;
  }
}

function openLoops(random: MazeRandom, columns: number, rows: number, openEast: boolean[], openSouth: boolean[]): void {
  const cells = columns * rows;
  const attempts = Math.max(1, Math.floor((cells * LOOPS_PER_HUNDRED_CELLS) / 100));
  for (let attempt = 0; attempt < attempts; attempt++) {
    const cell = random.below(cells);
    const south = random.below(2) === 1;
    if (south && Math.floor(cell / columns) < rows - 1) openSouth[cell] = true;
    else if (!south && cell % columns < columns - 1) openEast[cell] = true;
  }
}

function pickEntranceCell(random: MazeRandom, side: MazeSide, columns: number, rows: number): number {
  switch (side) {
    case "North":
      return random.below(columns);
    case "South":
      return (rows - 1) * columns + random.below(columns);
    case "West":
      return random.below(rows) * columns;
    default:
      return random.below(rows) * columns + columns - 1;
  }
}

/** The cell farthest from the start by the way through the maze; the lowest cell wins a tie. */
function farthest(columns: number, rows: number, start: number, openEast: boolean[], openSouth: boolean[]): number {
  const cells = columns * rows;
  const distance = new Array<number>(cells).fill(-1);
  const queue: number[] = [start];
  distance[start] = 0;
  for (let head = 0; head < queue.length; head++) {
    const cell = queue[head];
    const column = cell % columns;
    const row = Math.floor(cell / columns);
    const neighbors: number[] = [];
    if (row > 0 && openSouth[cell - columns]) neighbors.push(cell - columns);
    if (column < columns - 1 && openEast[cell]) neighbors.push(cell + 1);
    if (row < rows - 1 && openSouth[cell]) neighbors.push(cell + columns);
    if (column > 0 && openEast[cell - 1]) neighbors.push(cell - 1);
    for (const neighbor of neighbors) {
      if (distance[neighbor] >= 0) continue;
      distance[neighbor] = distance[cell] + 1;
      queue.push(neighbor);
    }
  }
  let best = start;
  for (let cell = 0; cell < cells; cell++) if (distance[cell] > distance[best]) best = cell;
  return best;
}

function layout(
  columns: number,
  rows: number,
  corridor: number,
  openEast: boolean[],
  openSouth: boolean[],
  entranceSide: MazeSide,
  entranceCell: number,
  prizeCell: number,
): MazeLayout {
  const pitch = corridor + 1;
  const blockColumns = columns * pitch + 1;
  const blockRows = rows * pitch + 1;

  const isWall = (column: number, row: number): boolean => {
    if (column < 0 || row < 0 || column >= blockColumns || row >= blockRows) return false;
    const inColumn = column % pitch;
    const inRow = row % pitch;
    if (inColumn !== 0 && inRow !== 0) return false;
    if (inColumn === 0 && inRow === 0) return true;
    if (inColumn === 0) {
      const line = column / pitch;
      const cellRow = Math.floor(row / pitch);
      if (line === 0) return !(entranceSide === "West" && entranceCell === cellRow * columns);
      if (line === columns) return !(entranceSide === "East" && entranceCell === cellRow * columns + columns - 1);
      return !openEast[cellRow * columns + line - 1];
    }
    const line = row / pitch;
    const cellColumn = Math.floor(column / pitch);
    if (line === 0) return !(entranceSide === "North" && entranceCell === cellColumn);
    if (line === rows) return !(entranceSide === "South" && entranceCell === (rows - 1) * columns + cellColumn);
    return !openSouth[(line - 1) * columns + cellColumn];
  };

  return {
    columns,
    rows,
    corridor,
    entranceSide,
    entranceCell,
    prizeCell,
    blockColumns,
    blockRows,
    isWall,
    cellCenter: (cell) => ({
      column: (cell % columns) * pitch + 1 + corridor / 2,
      row: Math.floor(cell / columns) * pitch + 1 + corridor / 2,
    }),
    toAscii: () => {
      const lines: string[] = [];
      for (let row = 0; row < blockRows; row++) {
        let line = "";
        for (let column = 0; column < blockColumns; column++) line += isWall(column, row) ? "#" : ".";
        lines.push(line);
      }
      return lines;
    },
  };
}
