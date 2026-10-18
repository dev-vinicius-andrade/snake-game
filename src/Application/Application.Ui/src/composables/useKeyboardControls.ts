import { getCurrentScope, onScopeDispose } from "vue";
import { initialKeyboardState, reduceKey, reduceMouse, releaseAll, type ControlIntent, type KeyResult } from "@/input/keyboard";

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

interface MouseEventLike {
  button: number;
  /** Set by browsers on mouse events that are only an echo of a touch. */
  sourceCapabilities?: { firesTouchEvents?: boolean } | null;
  target?: unknown;
  preventDefault(): void;
}

/** Clicks on buttons, links, inputs and dialogs belong to the page, not to the game. */
const PAGE_CONTROLS = "button, a, input, textarea, select, label, [role='dialog'], .v-overlay";

const isPageControl = (target: unknown) =>
  typeof (target as Element | null)?.closest === "function" && !!(target as Element).closest(PAGE_CONTROLS);

export interface UseKeyboardControlsOptions {
  onIntent(intent: ControlIntent): void;
  /** Defaults to `window`. */
  target?: KeyEventTarget;
  /** Keys are ignored (and left to the browser) while this returns false. */
  enabled?: () => boolean;
}

/** Listens to the keyboard and mouse buttons and reports what the player wants as device independent intents. */
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

  function onMouse(pressed: boolean) {
    return (event: MouseEventLike) => {
      if (!isEnabled() || isPageControl(event.target) || event.sourceCapabilities?.firesTouchEvents) return;
      const result = reduceMouse(state, { button: event.button, pressed });
      if (result.handled) event.preventDefault();
      apply(result);
    };
  }

  const onMouseDown = onMouse(true);
  const onMouseUp = onMouse(false);
  const onContextMenu = (event: { preventDefault(): void }) => {
    if (isEnabled()) event.preventDefault();
  };

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
    target().addEventListener("mousedown", onMouseDown);
    target().addEventListener("mouseup", onMouseUp);
    target().addEventListener("contextmenu", onContextMenu);
  }

  function detach() {
    if (!attached) return;
    attached = false;
    target().removeEventListener("keydown", onKeyDown);
    target().removeEventListener("keyup", onKeyUp);
    target().removeEventListener("blur", onBlur);
    target().removeEventListener("mousedown", onMouseDown);
    target().removeEventListener("mouseup", onMouseUp);
    target().removeEventListener("contextmenu", onContextMenu);
  }

  if (getCurrentScope()) onScopeDispose(detach);

  return { attach, detach };
}
