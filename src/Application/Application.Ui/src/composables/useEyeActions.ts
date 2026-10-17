import { shallowRef } from "vue";
import { EyeActions, type EyeActionsOptions } from "@/input/gaze/eyeActions";
import type { GazeSample, GazeSource } from "@/input/gaze/gazeSource";
import { HeadActions, type HeadActionsOptions } from "@/input/gaze/headActions";
import type { FaceMode } from "@/input/gaze/mediapipeGazeSource";
import type { TrackingState } from "@/input/gaze/trackingMonitor";
import type { ControlIntent } from "@/input/intents";
import type { RoomDimensions } from "@/types/game";
import { useGazeSubscription } from "./useGazeSubscription";

export interface UseEyeActionsOptions extends EyeActionsOptions {
  source: GazeSource;
  getRoom: () => RoomDimensions | null;
  onIntent: (intent: ControlIntent) => void;
  /** While this returns false nothing is shot or boosted, but the face is still followed. */
  enabled?: () => boolean;
  /** `eyes` (default): blink and far gaze. `head`: open mouth and raised eyebrows. */
  mode?: () => FaceMode;
  /** Tuning of the mouth and eyebrow actions. */
  head?: HeadActionsOptions;
}

const NO_ROOM = { width: 1, height: 1 };

/** Shoot and boost from camera samples, with the eyes or the face, and whether the face is tracked. */
export function useEyeActions(options: UseEyeActionsOptions) {
  const eyes = new EyeActions(options);
  const head = new HeadActions({ tracking: options.tracking, ...options.head });
  const tracking = shallowRef<TrackingState>("lost");
  let lastMode: FaceMode = options.mode?.() ?? "eyes";

  /** Ends whatever the mode we left was doing, so nothing keeps boosting. */
  function leave(previous: FaceMode) {
    (previous === "head" ? head : eyes).reset().forEach(options.onIntent);
  }

  function onSample(sample: GazeSample) {
    const mode = options.mode?.() ?? "eyes";
    if (mode !== lastMode) {
      leave(lastMode);
      lastMode = mode;
    }
    const room = options.getRoom();
    // Tracking is followed even while disabled or without a game (the start page): calibrating needs to know whether the face is found.
    const act = room !== null && (options.enabled?.() ?? true);
    const result = mode === "head" ? head.process(sample, { act }) : eyes.process(sample, room ?? NO_ROOM, { act });
    if (result.tracking) tracking.value = result.tracking;
    result.intents.forEach(options.onIntent);
  }

  const subscription = useGazeSubscription(options.source, onSample, {
    onStop: () => {
      eyes.reset().forEach(options.onIntent);
      head.reset().forEach(options.onIntent);
      tracking.value = "lost";
    },
  });

  return { ...subscription, tracking };
}
