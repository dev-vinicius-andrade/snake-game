import type { GamePoint, RoomDimensions } from "@/types/game";

export interface CameraSmootherOptions {
  /** How quickly the camera catches up with where it should be. Smaller is snappier. */
  timeConstantMs?: number;
  /** A target this far away (world units) in one step is a teleport, such as a respawn: the camera jumps to it. */
  jumpDistance?: number;
  /** A pause longer than this (a hidden tab) also jumps, instead of gliding for a long time. */
  maxStepMs?: number;
}

const DEFAULT_TIME_CONSTANT_MS = 100;
const DEFAULT_JUMP_DISTANCE = 700;
const DEFAULT_MAX_STEP_MS = 500;

function shortestDelta(delta: number, size: number): number {
  if (delta > size / 2) return delta - size;
  return delta < -size / 2 ? delta + size : delta;
}

/**
 * Makes the camera follow a point with a slight delay. A camera locked to the head shows every change of direction at full
 * strength: the whole world flips its scroll direction at once, and turning left and right in quick succession looks like a
 * shake. Following a little behind absorbs that.
 *
 * The easing is exponential, so it covers the same ground whatever the frame rate and however many times it is asked within one
 * frame. The world wraps, so the camera moves the short way round and its position is not wrapped: it keeps going past the edge,
 * which the renderer shows by drawing the world tiled.
 */
export class CameraSmoother {
  private readonly timeConstantMs: number;
  private readonly jumpDistance: number;
  private readonly maxStepMs: number;
  private current: GamePoint | null = null;
  private lastAt = 0;

  constructor(options: CameraSmootherOptions = {}) {
    this.timeConstantMs = options.timeConstantMs ?? DEFAULT_TIME_CONSTANT_MS;
    this.jumpDistance = options.jumpDistance ?? DEFAULT_JUMP_DISTANCE;
    this.maxStepMs = options.maxStepMs ?? DEFAULT_MAX_STEP_MS;
  }

  /** Where the camera is at `nowMs`, when it is heading for `target`. */
  step(target: GamePoint, nowMs: number, world: RoomDimensions): GamePoint {
    const elapsed = nowMs - this.lastAt;
    this.lastAt = nowMs;
    if (!this.current || elapsed > this.maxStepMs) return this.jumpTo(target);

    const dx = shortestDelta(target.x - this.current.x, world.width);
    const dy = shortestDelta(target.y - this.current.y, world.height);
    if (Math.hypot(dx, dy) > this.jumpDistance) return this.jumpTo(target);

    const factor = 1 - Math.exp(-Math.max(0, elapsed) / this.timeConstantMs);
    this.current = { x: this.current.x + dx * factor, y: this.current.y + dy * factor };
    return { ...this.current };
  }

  /** Forgets where the camera was: the next step starts exactly on its target. */
  reset() {
    this.current = null;
  }

  private jumpTo(target: GamePoint): GamePoint {
    this.current = { ...target };
    return { ...this.current };
  }
}
