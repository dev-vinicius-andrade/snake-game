import type { GamePoint } from "@/types/game";
import type { ControlIntent } from "./intents";

/** A floating stick: it appears where the thumb lands and steers by where the thumb goes from there. */
export interface StickState {
  origin: GamePoint | null;
  /** Angle last sent; null before the thumb has moved far enough. */
  angle: number | null;
}

export interface StickOptions {
  /** Distance (px) the thumb must be from where it landed before it steers. */
  deadZone?: number;
  /** Radians the thumb must turn before another steer is sent. */
  step?: number;
  /** Largest distance (px) the knob is drawn from the middle. */
  radius?: number;
}

export interface StickMove {
  state: StickState;
  intent?: ControlIntent;
  /** Knob offset from the stick's middle, inside the ring. */
  knob: GamePoint;
}

const DEAD_ZONE = 14;
const STEP = 0.05;
const RADIUS = 48;

export const startStick = (origin: GamePoint): StickState => ({ origin, angle: null });

export const endStick = (): StickState => ({ origin: null, angle: null });

const turnBetween = (a: number, b: number) => Math.abs(Math.atan2(Math.sin(a - b), Math.cos(a - b)));

export function moveStick(state: StickState, point: GamePoint, options: StickOptions = {}): StickMove {
  const { deadZone = DEAD_ZONE, step = STEP, radius = RADIUS } = options;
  if (!state.origin) return { state, knob: { x: 0, y: 0 } };
  const dx = point.x - state.origin.x;
  const dy = point.y - state.origin.y;
  const distance = Math.hypot(dx, dy);
  const reach = Math.min(distance, radius);
  const knob = distance === 0 ? { x: 0, y: 0 } : { x: (dx / distance) * reach, y: (dy / distance) * reach };
  if (distance < deadZone) return { state, knob };
  const angle = Math.atan2(dy, dx);
  const moved = state.angle === null || turnBetween(angle, state.angle) >= step;
  if (!moved) return { state, knob };
  return { state: { ...state, angle }, intent: { type: "steer", angle }, knob };
}
