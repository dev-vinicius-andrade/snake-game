import { getCurrentScope, onScopeDispose, shallowRef } from "vue";
import type { GazeSample, GazeSource } from "@/input/gaze/gazeSource";

export type GazeStatus = "off" | "starting" | "tracking" | "error";

/**
 * Starts and stops a gaze source and feeds its samples to a handler, with a status for the UI.
 * `onStart` runs before the source starts and `onStop` after it stopped, to reset whatever the handler keeps.
 */
export function useGazeSubscription(
  source: GazeSource,
  onSample: (sample: GazeSample) => void,
  { onStart = () => undefined, onStop = () => undefined }: { onStart?: () => void; onStop?: () => void } = {}
) {
  const status = shallowRef<GazeStatus>("off");
  const error = shallowRef<string | null>(null);
  let unsubscribe: (() => void) | null = null;

  async function start() {
    if (unsubscribe || status.value === "starting") return;
    status.value = "starting";
    error.value = null;
    onStart();
    try {
      await source.start();
    } catch (failure) {
      status.value = "error";
      error.value = failure instanceof Error ? failure.message : String(failure);
      return;
    }
    if (status.value !== "starting") {
      source.stop();
      return;
    }
    unsubscribe = source.subscribe(onSample);
    status.value = "tracking";
  }

  function stop() {
    unsubscribe?.();
    unsubscribe = null;
    source.stop();
    status.value = "off";
    onStop();
  }

  if (getCurrentScope()) onScopeDispose(stop);

  return { status, error, start, stop };
}
