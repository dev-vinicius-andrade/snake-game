import type { GamePoint, GameSnapshot, RoomDimensions } from "@/types/game";
import { mazePrizeCenter } from "./maze";

export interface PrizeCompass {
  /** Direction to the prize in radians: 0 is right, positive turns clockwise on screen. */
  angle: number;
  /** World units to the prize. */
  distance: number;
}

function shortestDelta(delta: number, size: number): number {
  if (delta > size / 2) return delta - size;
  return delta < -size / 2 ? delta + size : delta;
}

export function offsetTo(from: GamePoint, to: GamePoint, world: RoomDimensions): GamePoint {
  return { x: shortestDelta(to.x - from.x, world.width), y: shortestDelta(to.y - from.y, world.height) };
}

/**
 * Which way and how far the nearest prize is from the own snake, the short way round the world. While a maze stands the compass is for
 * its prize, at the far end, whatever else lies nearer. Null when there is none.
 */
export function prizeCompass(snapshot: GameSnapshot, selfGuid: string | null | undefined): PrizeCompass | null {
  const own = snapshot.snakes.find((snake) => snake.guid === selfGuid && snake.isAlive);
  if (!own) return null;
  const targets = snapshot.maze ? [mazePrizeCenter(snapshot.maze)] : snapshot.foods.filter((food) => food.kind === "prize").map((food) => food.position);
  const offsets = targets.map((target) => offsetTo(own.head, target, snapshot.room));
  if (offsets.length === 0) return null;
  const nearest = offsets.reduce((best, offset) => (Math.hypot(offset.x, offset.y) < Math.hypot(best.x, best.y) ? offset : best));
  return { angle: Math.atan2(nearest.y, nearest.x), distance: Math.hypot(nearest.x, nearest.y) };
}
