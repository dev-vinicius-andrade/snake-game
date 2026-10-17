import { shallowRef } from "vue";
import { EyeActions, type EyeActionsOptions } from "@/input/gaze/eyeActions";
import type { GazeSample, GazeSource } from "@/input/gaze/gazeSource";
import type { TrackingState } from "@/input/gaze/trackingMonitor";
import type { ControlIntent } from "@/input/intents";
import type { RoomDimensions } from "@/types/game";
import { useGazeSubscription } from "./useGazeSubscription";

export interface UseEyeActionsOptions extends EyeActionsOptions {
  source: GazeSource;
  getRoom: () => RoomDimensions | null;
  onIntent: (intent: ControlIntent) => void;
  /** Samples are ignored while this returns false. */
  enabled?: () => boolean;
}

/** Blink to shoot, far gaze to boost, and whether the face is tracked, from camera gaze samples. */
export function useEyeActions(options: UseEyeActionsOptions) {
  const actions = new EyeActions(options);
  const tracking = shallowRef<TrackingState>("lost");

  function onSample(sample: GazeSample) {
    if (!(options.enabled?.() ?? true)) return;
    const room = options.getRoom();
    if (!room) return;
    const result = actions.process(sample, room);
    if (result.tracking) tracking.value = result.tracking;
    result.intents.forEach(options.onIntent);
  }

  const subscription = useGazeSubscription(options.source, onSample, {
    onStop: () => {
      actions.reset().forEach(options.onIntent);
      tracking.value = "lost";
    },
  });

  return { ...subscription, tracking };
}
