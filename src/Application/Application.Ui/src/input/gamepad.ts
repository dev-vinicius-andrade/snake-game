import type { ControlDirection } from "@/types/game";
import type { ControlIntent } from "./intents";

/** The parts of a browser `Gamepad` the game reads, so specs need no browser. */
export interface GamepadReading {
  readonly axes: readonly number[];
  readonly buttons: ReadonlyArray<{ readonly pressed: boolean }>;
}

export interface GamepadState {
  /** Buttons held at the last poll, to tell a new press from a held one. */
  readonly held: ReadonlySet<number>;
  /** Angle last sent for the left stick; null while the stick rests. */
  readonly steerAngle: number | null;
  readonly boosting: boolean;
  readonly shooting: boolean;
}

export interface GamepadResult {
  state: GamepadState;
  intents: ControlIntent[];
}

export const initialGamepadState: GamepadState = { held: new Set(), steerAngle: null, boosting: false, shooting: false };

/** Left stick distance from the middle below which it counts as resting. */
const DEAD_ZONE = 0.35;
/** The stick must turn this far (radians) before another steer is sent. */
const STEER_STEP = 0.05;

// Standard mapping: https://w3c.github.io/gamepad/#remapping
const DPAD: ReadonlyMap<number, ControlDirection> = new Map([
  [12, "up"],
  [13, "down"],
  [14, "left"],
  [15, "right"],
]);
const SHOOT_BUTTONS: readonly number[] = [0, 5, 7];
const BOOST_BUTTONS: readonly number[] = [1, 4, 6];

const pressedButtons = (reading: GamepadReading): Set<number> =>
  new Set(reading.buttons.flatMap((button, index) => (button.pressed ? [index] : [])));

const risen = (now: ReadonlySet<number>, before: ReadonlySet<number>, buttons: Iterable<number>) =>
  [...buttons].filter((button) => now.has(button) && !before.has(button));

function steerIntent(reading: GamepadReading, last: number | null): { intents: ControlIntent[]; angle: number | null } {
  const x = reading.axes[0] ?? 0;
  const y = reading.axes[1] ?? 0;
  if (Math.hypot(x, y) < DEAD_ZONE) return { intents: [], angle: null };
  const angle = Math.atan2(y, x);
  const moved = last === null || Math.abs(Math.atan2(Math.sin(angle - last), Math.cos(angle - last))) >= STEER_STEP;
  return moved ? { intents: [{ type: "steer", angle }], angle } : { intents: [], angle: last };
}

/** Left stick steers, d-pad turns, A or a trigger or bumper shoots, B or the other trigger boosts. */
export function reduceGamepad(state: GamepadState, reading: GamepadReading): GamepadResult {
  const held = pressedButtons(reading);
  const steer = steerIntent(reading, state.steerAngle);
  const directions = risen(held, state.held, DPAD.keys()).map<ControlIntent>((button) => ({
    type: "direction",
    direction: DPAD.get(button)!,
  }));
  const shooting = SHOOT_BUTTONS.some((button) => held.has(button));
  const shots: ControlIntent[] = risen(held, state.held, SHOOT_BUTTONS).length > 0 ? [{ type: "shoot" }] : [];
  const release: ControlIntent[] = state.shooting && !shooting ? [{ type: "shootRelease" }] : [];
  const boosting = BOOST_BUTTONS.some((button) => held.has(button));
  const boost: ControlIntent[] = boosting !== state.boosting ? [{ type: "boost", active: boosting }] : [];
  return {
    state: { held, steerAngle: steer.angle, boosting, shooting },
    intents: [...steer.intents, ...directions, ...shots, ...release, ...boost],
  };
}
