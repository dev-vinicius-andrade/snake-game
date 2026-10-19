import type { GamePoint, GameSnapshot, StrikeKind, StrikeSnapshot } from "@/types/game";
import { DEFAULT_HEAD_SIZE } from "./pixelRenderer";

/** What the player is told about the strikes that are coming (lightning, meteors): what, how long is left, and whether the own snake stands in one. */
export interface StrikeWarning {
  /** What the own snake stands in the way of, or else what strikes first. */
  kind: StrikeKind;
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
export function strikeWarning(snapshot: GameSnapshot, selfGuid: string | null | undefined): StrikeWarning | null {
  if (snapshot.strikes.length === 0) return null;
  const bySoonest = [...snapshot.strikes].sort((first, second) => first.msLeft - second.msLeft);
  const threat = bySoonest.find((spot) => inSpot(snapshot, selfGuid, spot));
  return { kind: (threat ?? bySoonest[0]).kind, danger: threat !== undefined, msLeft: bySoonest[0].msLeft };
}

/** A warning ring: it grows from the centre (`progress` 0) to the full `radius` (1), where the strike falls. */
export interface StrikeRing {
  kind: StrikeKind;
  center: GamePoint;
  radius: number;
  progress: number;
}

/** The flash of a strike that fell: `age` goes from 0 (just now) to 1 (gone). */
export interface StrikeFlash {
  kind: StrikeKind;
  center: GamePoint;
  radius: number;
  age: number;
}

export interface StrikeView {
  rings: StrikeRing[];
  flashes: StrikeFlash[];
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
export class StrikeTracker {
  private seen = new Map<string, Seen>();
  private flashes: Array<{ kind: StrikeKind; center: GamePoint; radius: number; bornMs: number }> = [];

  observe(strikes: StrikeSnapshot[], nowMs: number) {
    const next = new Map<string, Seen>();
    for (const spot of strikes) {
      const key = `${spot.kind}:${spot.guid}`;
      const before = this.seen.get(key);
      next.set(key, { spot, total: Math.max(before?.total ?? 0, spot.msLeft, 1), seenAtMs: nowMs });
    }
    for (const [key, gone] of this.seen)
      if (!next.has(key)) this.flashes.push({ kind: gone.spot.kind, center: gone.spot.position, radius: gone.spot.radius, bornMs: nowMs });
    this.seen = next;
  }

  view(nowMs: number): StrikeView {
    this.flashes = this.flashes.filter((flash) => nowMs - flash.bornMs < FLASH_MS);
    return {
      rings: [...this.seen.values()].map(({ spot, total, seenAtMs }) => ({
        kind: spot.kind,
        center: spot.position,
        radius: spot.radius,
        progress: Math.min(1, Math.max(0, 1 - (spot.msLeft - (nowMs - seenAtMs)) / total)),
      })),
      flashes: this.flashes.map((flash) => ({ kind: flash.kind, center: flash.center, radius: flash.radius, age: (nowMs - flash.bornMs) / FLASH_MS })),
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

/** A circle as rows of pixels, widest in the middle: one run per row, as world positions on the pixel grid and a width in pixels. */
export function discRows(center: GamePoint, radius: number, pixelSize: number): Array<{ x: number; y: number; width: number }> {
  const rows: Array<{ x: number; y: number; width: number }> = [];
  const half = pixelSize / 2;
  for (let y = snap(center.y - radius, pixelSize); y <= center.y + radius; y += pixelSize) {
    const dy = y + half - center.y;
    if (Math.abs(dy) > radius) continue;
    const reach = Math.sqrt(radius * radius - dy * dy);
    const left = snap(center.x - reach, pixelSize);
    const width = Math.round((snap(center.x + reach, pixelSize) - left) / pixelSize) + 1;
    rows.push({ x: left, y, width });
  }
  return rows;
}
