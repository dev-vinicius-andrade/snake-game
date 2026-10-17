import type { GamePoint } from "@/types/game";
import { angleDistance } from "../angles";

export interface AimOptions {
  /** Distance from the head, in world units, the target must leave before steering starts. */
  deadRadius: number;
  /** Steering stops again below `deadRadius * releaseRatio`, so a target on the edge does not flicker. */
  releaseRatio?: number;
  /** Smallest heading change, in radians, worth reporting. */
  minAngleDelta?: number;
}

export interface AimState {
  readonly active: boolean;
  readonly lastAngle: number | null;
}

export interface AimResult {
  state: AimState;
  /** New heading in radians, or null when there is nothing to report. */
  steer: number | null;
}

export const initialAimState: AimState = { active: false, lastAngle: null };

const DEFAULT_RELEASE_RATIO = 0.8;
const DEFAULT_MIN_ANGLE_DELTA = 0.05;

function validate({ deadRadius, releaseRatio, minAngleDelta }: AimOptions) {
  if (!(deadRadius >= 0)) throw new RangeError("deadRadius must not be negative");
  if (releaseRatio !== undefined && !(releaseRatio > 0 && releaseRatio <= 1)) {
    throw new RangeError("releaseRatio must be in (0, 1]");
  }
  if (minAngleDelta !== undefined && !(minAngleDelta >= 0)) throw new RangeError("minAngleDelta must not be negative");
}

/**
 * Aim steering: the snake heads toward the target point. Close to the head the target is ignored
 * (dead zone, with hysteresis) and the snake keeps its heading. Angles are screen angles:
 * 0 is right and positive turns clockwise (y grows downwards), the same convention as the server.
 */
export function aimStep(state: AimState, head: GamePoint, target: GamePoint, options: AimOptions): AimResult {
  validate(options);
  const releaseRatio = options.releaseRatio ?? DEFAULT_RELEASE_RATIO;
  const minAngleDelta = options.minAngleDelta ?? DEFAULT_MIN_ANGLE_DELTA;

  const distance = Math.hypot(target.x - head.x, target.y - head.y);
  const threshold = state.active ? options.deadRadius * releaseRatio : options.deadRadius;
  if (distance < threshold) return { state: initialAimState, steer: null };

  const angle = Math.atan2(target.y - head.y, target.x - head.x);
  const changed = state.lastAngle === null || angleDistance(angle, state.lastAngle) >= minAngleDelta;
  return changed
    ? { state: { active: true, lastAngle: angle }, steer: angle }
    : { state: { active: true, lastAngle: state.lastAngle }, steer: null };
}
