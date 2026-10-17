import type { GamePoint } from "@/types/game";
import { angleDistance } from "../angles";

export interface AimOptions {
  /** Distance from the head, in world units, the target must leave before steering starts. */
  deadRadius: number;
  /** Steering stops again below `deadRadius * releaseRatio`, so a target on the edge does not flicker. */
  releaseRatio?: number;
  /** Smallest heading change, in radians, worth reporting. */
  minAngleDelta?: number;
  /**
   * How long the target must stay outside the dead zone, in one direction, before steering starts. A glance at
   * something else does not turn the snake. Zero (the default) steers at once, which suits a mouse.
   */
  dwellMs?: number;
  /** How far, in radians, the direction may wander during the dwell before the wait starts over. */
  dwellAngle?: number;
}

export interface AimState {
  readonly active: boolean;
  readonly lastAngle: number | null;
  /** While waiting out the dwell: when the current direction first showed up, and which direction it was. */
  readonly pendingSince?: number | null;
  readonly pendingAngle?: number | null;
}

export interface AimResult {
  state: AimState;
  /** New heading in radians, or null when there is nothing to report. */
  steer: number | null;
}

export const initialAimState: AimState = { active: false, lastAngle: null, pendingSince: null, pendingAngle: null };

const DEFAULT_RELEASE_RATIO = 0.8;
const DEFAULT_MIN_ANGLE_DELTA = 0.05;
const DEFAULT_DWELL_ANGLE = 0.35;

function validate({ deadRadius, releaseRatio, minAngleDelta, dwellMs, dwellAngle }: AimOptions) {
  if (!(deadRadius >= 0)) throw new RangeError("deadRadius must not be negative");
  if (releaseRatio !== undefined && !(releaseRatio > 0 && releaseRatio <= 1)) {
    throw new RangeError("releaseRatio must be in (0, 1]");
  }
  if (minAngleDelta !== undefined && !(minAngleDelta >= 0)) throw new RangeError("minAngleDelta must not be negative");
  if (dwellMs !== undefined && !(dwellMs >= 0)) throw new RangeError("dwellMs must not be negative");
  if (dwellAngle !== undefined && !(dwellAngle > 0)) throw new RangeError("dwellAngle must be greater than zero");
}

/** Keeps the time a direction first showed up, unless the direction wandered off, then restarts from now. */
function waitFor(state: AimState, angle: number, timestampMs: number, dwellAngle: number) {
  const sameDirection =
    state.pendingSince != null && state.pendingAngle != null && angleDistance(angle, state.pendingAngle) <= dwellAngle;
  return sameDirection ? { since: state.pendingSince!, angle: state.pendingAngle! } : { since: timestampMs, angle };
}

/**
 * Aim steering: the snake heads toward the target point. Close to the head the target is ignored
 * (dead zone, with hysteresis) and the snake keeps its heading. Angles are screen angles:
 * 0 is right and positive turns clockwise (y grows downwards), the same convention as the server.
 * With a dwell time the target has to stay put before the first turn, so glances do not steer.
 */
export function aimStep(state: AimState, head: GamePoint, target: GamePoint, options: AimOptions, timestampMs = 0): AimResult {
  validate(options);
  const releaseRatio = options.releaseRatio ?? DEFAULT_RELEASE_RATIO;
  const minAngleDelta = options.minAngleDelta ?? DEFAULT_MIN_ANGLE_DELTA;
  const dwellMs = options.dwellMs ?? 0;

  const distance = Math.hypot(target.x - head.x, target.y - head.y);
  const threshold = state.active ? options.deadRadius * releaseRatio : options.deadRadius;
  if (distance < threshold) return { state: initialAimState, steer: null };

  const angle = Math.atan2(target.y - head.y, target.x - head.x);
  if (!state.active && dwellMs > 0) {
    const pending = waitFor(state, angle, timestampMs, options.dwellAngle ?? DEFAULT_DWELL_ANGLE);
    if (timestampMs - pending.since < dwellMs) {
      return { state: { active: false, lastAngle: null, pendingSince: pending.since, pendingAngle: pending.angle }, steer: null };
    }
  }

  const changed = state.lastAngle === null || angleDistance(angle, state.lastAngle) >= minAngleDelta;
  return changed
    ? { state: { active: true, lastAngle: angle, pendingSince: null, pendingAngle: null }, steer: angle }
    : { state: { active: true, lastAngle: state.lastAngle, pendingSince: null, pendingAngle: null }, steer: null };
}
