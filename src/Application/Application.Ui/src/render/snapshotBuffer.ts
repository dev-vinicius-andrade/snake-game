import type { GamePoint, GameSnapshot, RoomDimensions, ShotSnapshot, SnakeSnapshot } from "@/types/game";

/** How far in the past the world is shown, so there is always a newer snapshot to blend toward. About two server updates. */
export const DEFAULT_DELAY_MS = 70;
/** Snapshots older than this (relative to the newest) are forgotten. */
const DEFAULT_MAX_AGE_MS = 1000;

/** A gap this long means the connection stalled: the timeline starts again from the arrival. */
const RESTART_AFTER_MS = 250;
/** An arrival this far from the expected time is not jitter but a real shift, and is believed at once. */
const MAX_JITTER_MS = 100;
/** How fast the estimate of the server's update interval follows the observed gaps. */
const INTERVAL_GAIN = 0.05;

const wrap = (value: number, size: number) => ((value % size) + size) % size;

function shortestDelta(delta: number, size: number): number {
  if (delta > size / 2) return delta - size;
  return delta < -size / 2 ? delta + size : delta;
}

/** A point part of the way (0..1) from `from` to `to`, going the short way round a world that wraps. */
function blendPoint(from: GamePoint, to: GamePoint, t: number, dimensions: RoomDimensions): GamePoint {
  return {
    x: wrap(from.x + shortestDelta(to.x - from.x, dimensions.width) * t, dimensions.width),
    y: wrap(from.y + shortestDelta(to.y - from.y, dimensions.height) * t, dimensions.height),
  };
}

/** Points are ordered tail to head, so they are matched from the head: a snake that grew or shrank keeps its head in step. */
function blendPath(from: GamePoint[], to: GamePoint[], t: number, dimensions: RoomDimensions): GamePoint[] {
  const blended = to.map((point) => point);
  const shared = Math.min(from.length, to.length);
  for (let back = 1; back <= shared; back++) {
    blended[to.length - back] = blendPoint(from[from.length - back], to[to.length - back], t, dimensions);
  }
  return blended;
}

function blendSnake(from: SnakeSnapshot | undefined, to: SnakeSnapshot, t: number, dimensions: RoomDimensions): SnakeSnapshot {
  if (!from) return to;
  const path = blendPath(from.path, to.path, t, dimensions);
  return { ...to, path, head: path[path.length - 1] ?? to.head };
}

function blendShot(from: ShotSnapshot | undefined, to: ShotSnapshot, t: number, dimensions: RoomDimensions): ShotSnapshot {
  return from ? { ...to, position: blendPoint(from.position, to.position, t, dimensions) } : to;
}

/**
 * The world part of the way (0..1) from `from` to `to`: snakes and shots are moved, everything else comes from `to`.
 * Neither snapshot is changed.
 */
export function blendSnapshots(from: GameSnapshot, to: GameSnapshot, t: number): GameSnapshot {
  const before = new Map(from.snakes.map((snake) => [snake.guid, snake]));
  const beforeShots = new Map(from.shots.map((shot) => [shot.guid, shot]));
  return {
    ...to,
    snakes: to.snakes.map((snake) => blendSnake(before.get(snake.guid), snake, t, to.room)),
    shots: to.shots.map((shot) => blendShot(beforeShots.get(shot.guid), shot, t, to.room)),
  };
}

export interface SnapshotBufferOptions {
  delayMs?: number;
  maxAgeMs?: number;
  /**
   * How much of each snapshot's lateness or earliness is believed (0..1). Left out, snapshots keep the time they arrived.
   * Set, snapshots are put on an even timeline at the server's real rate, which the arrival jitter does not shake.
   */
  jitterCorrection?: number;
}

interface Stamped {
  at: number;
  snapshot: GameSnapshot;
}

/**
 * Server snapshots arrive about 30 times a second and the screen is drawn 60 or more: drawn as they come, the world
 * moves in jumps, and every turn changes the direction of the jumps. The buffer keeps the last few snapshots with the
 * time they arrived, and shows the world a moment in the past blended between the two around that time.
 */
export class SnapshotBuffer {
  private readonly delayMs: number;
  private readonly maxAgeMs: number;
  private readonly jitterCorrection: number | undefined;
  private stamped: Stamped[] = [];
  private lastArrival = 0;
  private intervalMs = 0;

  constructor(options: SnapshotBufferOptions = {}) {
    this.delayMs = options.delayMs ?? DEFAULT_DELAY_MS;
    this.maxAgeMs = options.maxAgeMs ?? DEFAULT_MAX_AGE_MS;
    this.jitterCorrection = options.jitterCorrection;
  }

  get size() {
    return this.stamped.length;
  }

  /** Adds a snapshot that arrived at `atMs`. One that arrives with an earlier time than the newest is ignored. */
  push(snapshot: GameSnapshot, atMs: number) {
    const newest = this.stamped[this.stamped.length - 1];
    if (newest && atMs < this.lastArrival) return;
    const at = this.timelineTime(atMs, newest);
    if (newest && at === newest.at) this.stamped[this.stamped.length - 1] = { at, snapshot };
    else this.stamped.push({ at, snapshot });
    this.lastArrival = atMs;
    this.forgetOld(at);
  }

  /** Where on the shown timeline a snapshot that arrived at `atMs` goes: the expected next step, nudged toward its arrival. */
  private timelineTime(atMs: number, newest: Stamped | undefined): number {
    const gain = this.jitterCorrection;
    if (gain === undefined || !newest) return atMs;
    const gap = atMs - this.lastArrival;
    if (gap > RESTART_AFTER_MS) return atMs;
    this.intervalMs = this.intervalMs > 0 ? this.intervalMs + INTERVAL_GAIN * (gap - this.intervalMs) : gap;
    const expected = newest.at + this.intervalMs;
    const error = atMs - expected;
    if (Math.abs(error) > MAX_JITTER_MS) return atMs;
    return Math.max(expected + gain * error, newest.at + 1);
  }

  /** The world as it is shown at `nowMs`. Null before any snapshot. Never guesses ahead of the newest snapshot. */
  sample(nowMs: number): GameSnapshot | null {
    if (this.stamped.length === 0) return null;
    const shown = nowMs - this.delayMs;
    const newest = this.stamped[this.stamped.length - 1];
    if (shown >= newest.at) return newest.snapshot;

    const next = this.stamped.findIndex((entry) => entry.at >= shown);
    if (next <= 0) return this.stamped[0].snapshot;
    const before = this.stamped[next - 1];
    const after = this.stamped[next];
    const span = after.at - before.at;
    return blendSnapshots(before.snapshot, after.snapshot, span > 0 ? (shown - before.at) / span : 1);
  }

  clear() {
    this.stamped = [];
    this.intervalMs = 0;
  }

  private forgetOld(newestAt: number) {
    const limit = newestAt - this.maxAgeMs;
    while (this.stamped.length > 2 && this.stamped[0].at < limit) this.stamped.shift();
  }
}
