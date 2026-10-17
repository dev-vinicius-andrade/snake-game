import { getCurrentScope, onScopeDispose } from "vue";
import { initialKeyboardState, reduceKey, releaseAll, type ControlIntent, type KeyResult } from "@/input/keyboard";

type Listener = (event: any) => void;

/** What the composable needs from `window` (or any element), so it can be driven by a fake in specs. */
export interface KeyEventTarget {
  addEventListener(type: string, listener: Listener): void;
  removeEventListener(type: string, listener: Listener): void;
}

interface KeyEventLike {
  code: string;
  repeat?: boolean;
  preventDefault(): void;
}

export interface UseKeyboardControlsOptions {
  onIntent(intent: ControlIntent): void;
  /** Defaults to `window`. */
  target?: KeyEventTarget;
  /** Keys are ignored (and left to the browser) while this returns false. */
  enabled?: () => boolean;
}

/** Listens to the keyboard and reports what the player wants as device independent intents. */
export function useKeyboardControls(options: UseKeyboardControlsOptions) {
  let state = initialKeyboardState;
  let attached = false;

  const target = () => options.target ?? window;
  const isEnabled = () => options.enabled?.() ?? true;

  function apply(result: KeyResult) {
    state = result.state;
    result.intents.forEach((intent) => options.onIntent(intent));
  }

  function onKey(pressed: boolean) {
    return (event: KeyEventLike) => {
      if (!isEnabled()) return;
      const result = reduceKey(state, { code: event.code, pressed, repeat: event.repeat });
      if (result.handled) event.preventDefault();
      apply(result);
    };
  }

  const onKeyDown = onKey(true);
  const onKeyUp = onKey(false);
  const onBlur = () => apply(releaseAll(state));

  function attach() {
    if (attached) return;
    attached = true;
    state = initialKeyboardState;
    target().addEventListener("keydown", onKeyDown);
    target().addEventListener("keyup", onKeyUp);
    target().addEventListener("blur", onBlur);
  }

  function detach() {
    if (!attached) return;
    attached = false;
    target().removeEventListener("keydown", onKeyDown);
    target().removeEventListener("keyup", onKeyUp);
    target().removeEventListener("blur", onBlur);
  }

  if (getCurrentScope()) onScopeDispose(detach);

  return { attach, detach };
}
