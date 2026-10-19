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
/** How long the bits thrown off a struck body fly. */
export const SPLASH_MS = 700;
/** The most spots of bodies one strike splashes. */
const MAX_SPLASH_POINTS = 40;
/** The farthest a bit flies, in world units. */
const SPLASH_REACH = 70;
const SPLASH_PARTICLES = 8;

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

/** Bits thrown off a body that was struck: `age` goes from 0 (just now) to 1 (gone). */
export interface StrikeSplash {
  kind: StrikeKind;
  center: GamePoint;
  age: number;
  /** Makes the bits of one splash fly differently from those of another, the same every frame. */
  seed: number;
}

export interface StrikeView {
  rings: StrikeRing[];
  flashes: StrikeFlash[];
  splashes: StrikeSplash[];
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
  private splashes: Array<{ kind: StrikeKind; center: GamePoint; bornMs: number; seed: number }> = [];
  private previous: GameSnapshot | null = null;
  private gone: StrikeSnapshot[] = [];

  observe(strikes: StrikeSnapshot[], nowMs: number) {
    const next = new Map<string, Seen>();
    for (const spot of strikes) {
      const key = `${spot.kind}:${spot.guid}`;
      const before = this.seen.get(key);
      next.set(key, { spot, total: Math.max(before?.total ?? 0, spot.msLeft, 1), seenAtMs: nowMs });
    }
    this.gone = [];
    for (const [key, gone] of this.seen)
      if (!next.has(key)) {
        this.flashes.push({ kind: gone.spot.kind, center: gone.spot.position, radius: gone.spot.radius, bornMs: nowMs });
        this.gone.push(gone.spot);
      }
    this.seen = next;
  }

  /**
   * Like observe, for a whole snapshot: the bodies the previous snapshot had inside a strike that is gone are splashed, each segment
   * of them. Every player sees this, since it comes from what the snapshots say and not from a message to the one that was hit.
   */
  observeFrame(snapshot: GameSnapshot, nowMs: number) {
    this.observe(snapshot.strikes, nowMs);
    const before = this.previous;
    if (before)
      for (const spot of this.gone)
        for (const center of splashPoints(before, spot))
          this.splashes.push({ kind: spot.kind, center, bornMs: nowMs, seed: Math.abs(Math.round(center.x * 31 + center.y * 17)) });
    this.previous = snapshot;
  }

  view(nowMs: number): StrikeView {
    this.flashes = this.flashes.filter((flash) => nowMs - flash.bornMs < FLASH_MS);
    this.splashes = this.splashes.filter((splash) => nowMs - splash.bornMs < SPLASH_MS);
    return {
      rings: [...this.seen.values()].map(({ spot, total, seenAtMs }) => ({
        kind: spot.kind,
        center: spot.position,
        radius: spot.radius,
        progress: Math.min(1, Math.max(0, 1 - (spot.msLeft - (nowMs - seenAtMs)) / total)),
      })),
      splashes: this.splashes
        .filter((splash) => nowMs - splash.bornMs < SPLASH_MS)
        .map((splash) => ({ kind: splash.kind, center: splash.center, age: (nowMs - splash.bornMs) / SPLASH_MS, seed: splash.seed })),
      flashes: this.flashes.map((flash) => ({ kind: flash.kind, center: flash.center, radius: flash.radius, age: (nowMs - flash.bornMs) / FLASH_MS })),
    };
  }

  clear() {
    this.seen = new Map();
    this.flashes = [];
    this.splashes = [];
    this.previous = null;
    this.gone = [];
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

/** The middle of every segment of every living snake that is inside a strike, the short way round the world. At most a handful. */
export function splashPoints(snapshot: GameSnapshot, spot: Pick<StrikeSnapshot, "position" | "radius">): GamePoint[] {
  const half = DEFAULT_HEAD_SIZE / 2;
  const points: GamePoint[] = [];
  for (const snake of snapshot.snakes) {
    if (!snake.isAlive) continue;
    for (const segment of snake.path) {
      const middle = { x: segment.x + half, y: segment.y + half };
      const dx = shortest(middle.x - spot.position.x, snapshot.room.width);
      const dy = shortest(middle.y - spot.position.y, snapshot.room.height);
      if (Math.hypot(dx, dy) <= spot.radius) points.push(middle);
      if (points.length >= MAX_SPLASH_POINTS) return points;
    }
  }
  return points;
}

const unit = (seed: number, index: number) => {
  const value = Math.sin(seed * 12.9898 + index * 78.233) * 43758.5453;
  return value - Math.floor(value);
};

/** The bits of a splash at its age: where each is, and how bright (0 brightest, 2 dimmest). The same every time for the same splash. */
export function splashParticles(splash: StrikeSplash): Array<GamePoint & { shade: number }> {
  const age = Math.min(1, Math.max(0, splash.age));
  const travelled = 1 - (1 - age) * (1 - age);
  const shade = age < 0.35 ? 0 : age < 0.7 ? 1 : 2;
  return Array.from({ length: SPLASH_PARTICLES }, (_, index) => {
    const angle = (index / SPLASH_PARTICLES) * 2 * Math.PI + (unit(splash.seed, index) - 0.5) * 0.8;
    const reach = SPLASH_REACH * (0.5 + 0.5 * unit(splash.seed, index + 31));
    return { x: splash.center.x + Math.cos(angle) * reach * travelled, y: splash.center.y + Math.sin(angle) * reach * travelled, shade };
  });
}
