import type { GamePoint, GameSnapshot, RoomDimensions, ZonePhase, ZoneSnapshot } from "@/types/game";
import { DEFAULT_HEAD_SIZE } from "./pixelRenderer";

const shortest = (delta: number, size: number) => (delta > size / 2 ? delta - size : delta < -size / 2 ? delta + size : delta);

/** Whether a head is outside the zone, measured from the middle of the head, the short way round the world when `world` is given. The edge is inside. */
export function isOutsideZone(head: GamePoint, zone: ZoneSnapshot, world?: RoomDimensions): boolean {
  const half = DEFAULT_HEAD_SIZE / 2;
  const dx = head.x + half - zone.position.x;
  const dy = head.y + half - zone.position.y;
  const reach = world ? Math.hypot(shortest(dx, world.width), shortest(dy, world.height)) : Math.hypot(dx, dy);
  return reach > zone.radius;
}

/** What the player is told about the safe zone: the phase, the time left in it, and whether the own snake is outside and paying for it. */
export interface ZoneWarning {
  phase: ZonePhase;
  msLeft: number;
  outside: boolean;
}

/** Null while there is no zone. Nobody pays while it only warns, so nobody is outside then. */
export function zoneWarning(snapshot: GameSnapshot, selfGuid: string | null | undefined): ZoneWarning | null {
  const zone = snapshot.zone;
  if (!zone) return null;
  const own = snapshot.snakes.find((snake) => snake.guid === selfGuid && snake.isAlive);
  return {
    phase: zone.phase,
    msLeft: zone.msLeft,
    outside: zone.phase !== "warning" && own !== undefined && isOutsideZone(own.head, zone, snapshot.room),
  };
}
