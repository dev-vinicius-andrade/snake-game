/** A queue of toasts shown one at a time, each for a while. Pure: the caller owns the clock. */
export interface ToastQueueState {
  current: string | null;
  /** When `current` goes away (or when the gap after it ends), in clock milliseconds. */
  until: number;
  waiting: string[];
}

export const TOAST_MS = 4000;
/** Gap between two toasts, so the next one reads as new. */
export const TOAST_GAP_MS = 300;

export const emptyToastQueue = (): ToastQueueState => ({ current: null, until: 0, waiting: [] });

/** Adds ids (ones already waiting or showing are skipped) and shows the first at once when idle. */
export function enqueueToasts(state: ToastQueueState, ids: readonly string[], now: number): ToastQueueState {
  const known = new Set([state.current, ...state.waiting]);
  const fresh = [...new Set(ids)].filter((id) => !known.has(id));
  return advanceToasts({ ...state, waiting: [...state.waiting, ...fresh] }, now);
}

/** Drops the current toast when its time is up and starts the next one after the gap. */
export function advanceToasts(state: ToastQueueState, now: number, durationMs = TOAST_MS, gapMs = TOAST_GAP_MS): ToastQueueState {
  if (state.current !== null) {
    if (now < state.until) return state;
    return advanceToasts({ ...state, current: null, until: state.until + gapMs }, now, durationMs, gapMs);
  }
  if (state.waiting.length === 0 || now < state.until) return state;
  const [next, ...rest] = state.waiting;
  return { current: next, until: now + durationMs, waiting: rest };
}

/** Milliseconds until the state would change, or null when it never will without new toasts. */
export function nextToastChange(state: ToastQueueState, now: number): number | null {
  if (state.current === null && state.waiting.length === 0) return null;
  return Math.max(0, state.until - now);
}
