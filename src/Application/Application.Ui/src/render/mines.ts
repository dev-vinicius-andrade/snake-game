import type { GamePoint, GameSnapshot, MineSnapshot } from "@/types/game";
import { DEFAULT_HEAD_SIZE } from "./pixelRenderer";

/** How long the blast of a mine lasts on screen. */
export const BLAST_MS = 500;
/** A snake is near a mine when its head is within this many blast radii of it. */
const NEAR_RADII = 2;

/** A mine on the map: where, how far its blast reaches, and whether it is armed yet. */
export interface MineMark {
  center: GamePoint;
  radius: number;
  armed: boolean;
}

/** The blast of a mine that went off: `age` goes from 0 (just now) to 1 (gone). */
export interface MineBlast {
  center: GamePoint;
  radius: number;
  age: number;
}

export interface MineView {
  mines: MineMark[];
  blasts: MineBlast[];
}

interface Seen {
  mine: MineSnapshot;
  seenAtMs: number;
}

/**
 * Keeps the mines between snapshots. The server says how long each has until it is armed; the tracker counts on from there, and
 * notices a mine that is gone from the newest snapshot: a snake touched it, so it blows up. Like food, the place is its identity.
 */
export class MineTracker {
  private seen = new Map<string, Seen>();
  private blasts: Array<{ center: GamePoint; radius: number; bornMs: number }> = [];

  observe(mines: MineSnapshot[], nowMs: number) {
    const next = new Map<string, Seen>();
    for (const mine of mines) next.set(mine.guid, { mine, seenAtMs: nowMs });
    for (const [key, gone] of this.seen)
      if (!next.has(key)) this.blasts.push({ center: gone.mine.position, radius: gone.mine.radius, bornMs: nowMs });
    this.seen = next;
  }

  view(nowMs: number): MineView {
    this.blasts = this.blasts.filter((blast) => nowMs - blast.bornMs < BLAST_MS);
    return {
      mines: [...this.seen.values()].map(({ mine, seenAtMs }) => ({
        center: mine.position,
        radius: mine.radius,
        armed: mine.msToArm - (nowMs - seenAtMs) <= 0,
      })),
      blasts: this.blasts.map((blast) => ({ center: blast.center, radius: blast.radius, age: (nowMs - blast.bornMs) / BLAST_MS })),
    };
  }

  clear() {
    this.seen = new Map();
    this.blasts = [];
  }
}

const shortest = (delta: number, size: number) => (delta > size / 2 ? delta - size : delta < -size / 2 ? delta + size : delta);

/** Whether the head of any living snake is close to a mine (within twice its blast), the short way round the world. */
export function isMineNear(mark: MineMark, snapshot: GameSnapshot): boolean {
  const half = DEFAULT_HEAD_SIZE / 2;
  const reach = mark.radius * NEAR_RADII;
  return snapshot.snakes.some((snake) => {
    if (!snake.isAlive) return false;
    const dx = shortest(snake.head.x + half - mark.center.x, snapshot.room.width);
    const dy = shortest(snake.head.y + half - mark.center.y, snapshot.room.height);
    return Math.hypot(dx, dy) <= reach;
  });
}
