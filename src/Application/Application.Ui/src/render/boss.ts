import type { BossSnapshot, GamePoint, GameSnapshot } from "@/types/game";
import { offsetTo } from "./prizeCompass";

export interface BossCompass {
  /** Direction to the boss in radians: 0 is right, positive turns clockwise on screen. */
  angle: number;
  /** World units to the boss. */
  distance: number;
  /** Hit points left, 0..1. */
  hpRatio: number;
}

/** Which way and how far the boss is from the own snake, the short way round the world, and how hurt it is. Null without one. */
export function bossCompass(snapshot: GameSnapshot, selfGuid: string | null | undefined): BossCompass | null {
  const own = snapshot.snakes.find((snake) => snake.guid === selfGuid && snake.isAlive);
  if (!snapshot.boss || !own) return null;
  const offset = offsetTo(own.head, snapshot.boss.position, snapshot.room);
  return { angle: Math.atan2(offset.y, offset.x), distance: Math.hypot(offset.x, offset.y), hpRatio: snapshot.boss.hpRatio };
}

const snap = (value: number, pixelSize: number) => Math.floor(value / pixelSize) * pixelSize;

/** How far a barrel reaches past the edge of the body, in world units. */
const BARREL_REACH = 20;

/** The pixels of the barrels, from the hub out past the body: world positions on the pixel grid, none twice, the hub itself left out. */
export function armPixels(boss: BossSnapshot, pixelSize: number): GamePoint[] {
  const found = new Map<string, GamePoint>();
  const length = boss.size / 2 + BARREL_REACH;
  for (let arm = 0; arm < boss.arms; arm++) {
    const angle = boss.angle + (arm * 2 * Math.PI) / boss.arms;
    for (let along = pixelSize; along <= length; along += pixelSize) {
      const point = { x: snap(boss.position.x + Math.cos(angle) * along, pixelSize), y: snap(boss.position.y + Math.sin(angle) * along, pixelSize) };
      found.set(`${point.x},${point.y}`, point);
    }
  }
  return [...found.values()];
}

/** Gap between the top of the boss and its hit point bar, in world units. */
const BAR_GAP = 15;

/** The hit point bar above the boss: as wide as the boss, in pixels, and how many of them are full. It shows at least one while the boss stands. */
export function hpBarPixels(boss: BossSnapshot, pixelSize: number): { origin: GamePoint; width: number; filled: number } {
  const width = Math.max(1, Math.round(boss.size / pixelSize));
  return {
    origin: { x: snap(boss.position.x - boss.size / 2, pixelSize), y: snap(boss.position.y - boss.size / 2 - BAR_GAP, pixelSize) },
    width,
    filled: Math.min(width, Math.max(1, Math.ceil(boss.hpRatio * width))),
  };
}
