import type { ControlDirection } from "@/types/game";
import type { ControlIntent } from "./intents";

export type { ControlIntent };

export interface KeyboardState {
  /** Direction key codes currently held, oldest first. The last one decides the direction. */
  readonly held: readonly string[];
  readonly boosting: boolean;
  /** A shoot key or mouse button is held. */
  readonly shooting: boolean;
}

export interface KeyInput {
  code: string;
  pressed: boolean;
  repeat?: boolean;
}

export interface KeyResult {
  state: KeyboardState;
  intents: ControlIntent[];
  /** True when the key means something to the game, so the browser default should be prevented. */
  handled: boolean;
}

export const initialKeyboardState: KeyboardState = { held: [], boosting: false, shooting: false };

const DIRECTION_KEYS: ReadonlyMap<string, ControlDirection> = new Map([
  ["ArrowUp", "up"],
  ["KeyW", "up"],
  ["ArrowDown", "down"],
  ["KeyS", "down"],
  ["ArrowLeft", "left"],
  ["KeyA", "left"],
  ["ArrowRight", "right"],
  ["KeyD", "right"],
]);
const SHOOT_KEYS: ReadonlySet<string> = new Set(["Space"]);
const BOOST_KEYS: ReadonlySet<string> = new Set(["ShiftLeft", "ShiftRight"]);

export const directionForKey = (code: string): ControlDirection | null => DIRECTION_KEYS.get(code) ?? null;

const activeDirection = (held: readonly string[]): ControlDirection | null =>
  held.length === 0 ? null : directionForKey(held[held.length - 1]);

function reduceDirectionKey(state: KeyboardState, { code, pressed }: KeyInput): KeyResult {
  const held = pressed
    ? state.held.includes(code)
      ? state.held
      : [...state.held, code]
    : state.held.filter((heldCode) => heldCode !== code);
  const before = activeDirection(state.held);
  const after = activeDirection(held);
  const intents: ControlIntent[] = after && after !== before ? [{ type: "direction", direction: after }] : [];
  const changed = held.length !== state.held.length;
  return { state: changed ? { ...state, held } : state, intents, handled: true };
}

function reduceBoostKey(state: KeyboardState, { pressed }: KeyInput): KeyResult {
  if (state.boosting === pressed) return { state, intents: [], handled: true };
  return { state: { ...state, boosting: pressed }, intents: [{ type: "boost", active: pressed }], handled: true };
}

function reduceShoot(state: KeyboardState, pressed: boolean, repeat: boolean): KeyResult {
  if (pressed) {
    const fresh = !repeat && !state.shooting;
    return {
      state: fresh ? { ...state, shooting: true } : state,
      intents: fresh ? [{ type: "shoot" }] : [],
      handled: true,
    };
  }
  if (!state.shooting) return { state, intents: [], handled: true };
  return { state: { ...state, shooting: false }, intents: [{ type: "shootRelease" }], handled: true };
}

export function reduceKey(state: KeyboardState, input: KeyInput): KeyResult {
  if (DIRECTION_KEYS.has(input.code)) return reduceDirectionKey(state, input);
  if (SHOOT_KEYS.has(input.code)) return reduceShoot(state, input.pressed, input.repeat === true);
  if (BOOST_KEYS.has(input.code)) return reduceBoostKey(state, input);
  return { state, intents: [], handled: false };
}

export interface MouseInput {
  button: number;
  pressed: boolean;
}

const MOUSE_SHOOT = 0;
const MOUSE_BOOST = 2;

/** Left click shoots, holding the right button boosts. */
export function reduceMouse(state: KeyboardState, input: MouseInput): KeyResult {
  if (input.button === MOUSE_SHOOT) return reduceShoot(state, input.pressed, false);
  if (input.button === MOUSE_BOOST) return reduceBoostKey(state, { code: "", pressed: input.pressed });
  return { state, intents: [], handled: false };
}

/** Window lost focus: key-up events will never arrive, so forget everything that is held. */
export function releaseAll(state: KeyboardState): KeyResult {
  const intents: ControlIntent[] = [
    ...(state.boosting ? [{ type: "boost", active: false } as const] : []),
    ...(state.shooting ? [{ type: "shootRelease" } as const] : []),
  ];
  return { state: initialKeyboardState, intents, handled: true };
}
