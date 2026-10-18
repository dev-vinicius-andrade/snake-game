import type { GamePoint } from "@/types/game";
import { DEFAULT_PIXEL_SIZE } from "./pixelRenderer";

export interface CameraSmootherOptions {
  /** How quickly the camera catches up with where it should be. Smaller is snappier. */
  timeConstantMs?: number;
  /** A target this far away (world units) in one step is a teleport, such as a respawn: the camera jumps to it. */
  jumpDistance?: number;
  /** A pause longer than this (a hidden tab) also jumps, instead of gliding for a long time. */
  maxStepMs?: number;
  /** Results are rounded to a multiple of this, so the pixel art moves in whole logical pixels. 0 turns it off. */
  snapTo?: number;
}

const DEFAULT_TIME_CONSTANT_MS = 90;
const DEFAULT_JUMP_DISTANCE = 1500;
const DEFAULT_MAX_STEP_MS = 500;

/**
 * Eases the camera toward the position it should have. Server snapshots arrive about 30 times a second and the camera
 * stops dead at the walls; easing turns both into an even glide. The easing is exponential, so it covers the same ground
 * whatever the frame rate, and however many times it is asked inside one frame.
 */
export class CameraSmoother {
  private readonly timeConstantMs: number;
  private readonly jumpDistance: number;
  private readonly maxStepMs: number;
  private readonly snapTo: number;
  private current: GamePoint | null = null;
  private lastAt = 0;

  constructor(options: CameraSmootherOptions = {}) {
    this.timeConstantMs = options.timeConstantMs ?? DEFAULT_TIME_CONSTANT_MS;
    this.jumpDistance = options.jumpDistance ?? DEFAULT_JUMP_DISTANCE;
    this.maxStepMs = options.maxStepMs ?? DEFAULT_MAX_STEP_MS;
    this.snapTo = options.snapTo ?? DEFAULT_PIXEL_SIZE;
  }

  /** Where the camera is at `nowMs`, when it is heading for `target`. */
  step(target: GamePoint, nowMs: number): GamePoint {
    const elapsed = nowMs - this.lastAt;
    this.lastAt = nowMs;
    if (!this.current || elapsed > this.maxStepMs || this.distanceTo(target) > this.jumpDistance) {
      this.current = { ...target };
      return this.output();
    }

    const factor = 1 - Math.exp(-Math.max(0, elapsed) / this.timeConstantMs);
    this.current = {
      x: this.current.x + (target.x - this.current.x) * factor,
      y: this.current.y + (target.y - this.current.y) * factor,
    };
    return this.output();
  }

  /** Forgets where the camera was: the next step starts exactly on its target. */
  reset() {
    this.current = null;
  }

  private distanceTo(target: GamePoint): number {
    return Math.hypot(target.x - this.current!.x, target.y - this.current!.y);
  }

  private output(): GamePoint {
    const { x, y } = this.current!;
    if (!(this.snapTo > 0)) return { x, y };
    return { x: Math.round(x / this.snapTo) * this.snapTo, y: Math.round(y / this.snapTo) * this.snapTo };
  }
}
