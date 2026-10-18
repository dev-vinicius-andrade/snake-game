import { getCurrentScope, onScopeDispose } from "vue";
import { initialGamepadState, reduceGamepad, type GamepadReading } from "@/input/gamepad";
import type { ControlIntent } from "@/input/intents";

const NO_PAD: GamepadReading = { axes: [], buttons: [] };

export interface UseGamepadControlsOptions {
  onIntent(intent: ControlIntent): void;
  /** Reading is skipped (and nothing is reported) while this returns false. */
  enabled?: () => boolean;
  /** The first connected pad, or null. Defaults to the browser's gamepads. */
  read?: () => GamepadReading | null;
  /** Defaults to `requestAnimationFrame`. */
  schedule?: (callback: () => void) => number;
  /** Defaults to `cancelAnimationFrame`. */
  cancel?: (handle: number) => unknown;
}

const firstConnectedPad = (): GamepadReading | null =>
  Array.from(navigator.getGamepads?.() ?? []).find((pad): pad is Gamepad => !!pad?.connected) ?? null;

/** Polls a game controller every frame and reports what the player wants as device independent intents. */
export function useGamepadControls(options: UseGamepadControlsOptions) {
  const read = options.read ?? firstConnectedPad;
  const hasFrames = typeof requestAnimationFrame === "function";
  const schedule = options.schedule ?? ((callback) => (hasFrames ? requestAnimationFrame(callback) : 0));
  const cancel = options.cancel ?? ((handle) => hasFrames && cancelAnimationFrame(handle));
  let state = initialGamepadState;
  let handle: number | null = null;

  function poll() {
    handle = schedule(poll);
    if (!(options.enabled?.() ?? true)) return;
    const result = reduceGamepad(state, read() ?? NO_PAD);
    state = result.state;
    result.intents.forEach((intent) => options.onIntent(intent));
  }

  function attach() {
    if (handle !== null) return;
    state = initialGamepadState;
    handle = schedule(poll);
  }

  function detach() {
    if (handle === null) return;
    cancel(handle);
    handle = null;
  }

  if (getCurrentScope()) onScopeDispose(detach);

  return { attach, detach };
}
