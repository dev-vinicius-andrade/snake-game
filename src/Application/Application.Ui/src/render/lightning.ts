import type { GamePoint, GameSnapshot, StrikeSnapshot } from "@/types/game";
import { DEFAULT_HEAD_SIZE } from "./pixelRenderer";

/** What the player is told about the lightning that is about to strike: how long is left, and whether the own snake stands in it. */
export interface LightningWarning {
  danger: boolean;
  /** Milliseconds to the first strike, as of the snapshot. */
  msLeft: number;
}

/** How long the flash of a strike lasts on screen. */
export const FLASH_MS = 450;

const shortest = (delta: number, size: number) => (delta > size / 2 ? delta - size : delta < -size / 2 ? delta + size : delta);

/** Whether the own living snake has its head in a marked spot, the short way round the world. */
function inSpot(snapshot: GameSnapshot, selfGuid: string | null | undefined, spot: StrikeSnapshot): boolean {
  const own = snapshot.snakes.find((snake) => snake.guid === selfGuid && snake.isAlive);
  if (!own) return false;
  const half = DEFAULT_HEAD_SIZE / 2;
  const dx = shortest(own.head.x + half - spot.position.x, snapshot.room.width);
  const dy = shortest(own.head.y + half - spot.position.y, snapshot.room.height);
  return Math.hypot(dx, dy) <= spot.radius;
}

/** Null while no spot is marked. */
export function lightningWarning(snapshot: GameSnapshot, selfGuid: string | null | undefined): LightningWarning | null {
  if (snapshot.strikes.length === 0) return null;
  return {
    danger: snapshot.strikes.some((spot) => inSpot(snapshot, selfGuid, spot)),
    msLeft: Math.min(...snapshot.strikes.map((spot) => spot.msLeft)),
  };
}

/** A warning ring: it grows from the centre (`progress` 0) to the full `radius` (1), where the strike falls. */
export interface LightningRing {
  center: GamePoint;
  radius: number;
  progress: number;
}

/** The flash of a strike that fell: `age` goes from 0 (just now) to 1 (gone). */
export interface LightningFlash {
  center: GamePoint;
  radius: number;
  age: number;
}

export interface LightningView {
  rings: LightningRing[];
  flashes: LightningFlash[];
}

interface Seen {
  spot: StrikeSnapshot;
  /** The time left when first seen: what the ring grows over. */
  total: number;
  seenAtMs: number;
}

/**
 * Keeps the lightning warnings between snapshots. The server says how long each has left, 30 times a second; the tracker counts on
 * from there so the ring grows smoothly, and notices a warning that is gone from the newest snapshot: the strike fell, so it flashes.
 */
export class LightningTracker {
  private seen = new Map<string, Seen>();
  private flashes: Array<{ center: GamePoint; radius: number; bornMs: number }> = [];

  observe(strikes: StrikeSnapshot[], nowMs: number) {
    const next = new Map<string, Seen>();
    for (const spot of strikes) {
      const before = this.seen.get(spot.guid);
      next.set(spot.guid, { spot, total: Math.max(before?.total ?? 0, spot.msLeft, 1), seenAtMs: nowMs });
    }
    for (const [key, gone] of this.seen)
      if (!next.has(key)) this.flashes.push({ center: gone.spot.position, radius: gone.spot.radius, bornMs: nowMs });
    this.seen = next;
  }

  view(nowMs: number): LightningView {
    this.flashes = this.flashes.filter((flash) => nowMs - flash.bornMs < FLASH_MS);
    return {
      rings: [...this.seen.values()].map(({ spot, total, seenAtMs }) => ({
        center: spot.position,
        radius: spot.radius,
        progress: Math.min(1, Math.max(0, 1 - (spot.msLeft - (nowMs - seenAtMs)) / total)),
      })),
      flashes: this.flashes.map((flash) => ({ center: flash.center, radius: flash.radius, age: (nowMs - flash.bornMs) / FLASH_MS })),
    };
  }

  clear() {
    this.seen = new Map();
    this.flashes = [];
  }
}

const snap = (value: number, pixelSize: number) => Math.floor(value / pixelSize) * pixelSize;

/** The pixels of a circle's outline, in order round it: world positions on the pixel grid, none twice. */
export function ringPoints(center: GamePoint, radius: number, pixelSize: number): GamePoint[] {
  const steps = Math.max(8, Math.ceil(((2 * Math.PI * radius) / pixelSize) * 2));
  const found = new Map<string, GamePoint>();
  for (let step = 0; step < steps; step++) {
    const angle = (step / steps) * 2 * Math.PI;
    const point = { x: snap(center.x + Math.cos(angle) * radius, pixelSize), y: snap(center.y + Math.sin(angle) * radius, pixelSize) };
    found.set(`${point.x},${point.y}`, point);
  }
  return [...found.values()];
}

/** The pixels inside a circle, as world positions on the pixel grid. */
export function discPoints(center: GamePoint, radius: number, pixelSize: number): GamePoint[] {
  const points: GamePoint[] = [];
  const half = pixelSize / 2;
  for (let x = snap(center.x - radius, pixelSize); x <= center.x + radius; x += pixelSize)
    for (let y = snap(center.y - radius, pixelSize); y <= center.y + radius; y += pixelSize)
      if (Math.hypot(x + half - center.x, y + half - center.y) <= radius) points.push({ x, y });
  return points;
}
