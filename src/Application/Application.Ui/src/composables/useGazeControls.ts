import { getCurrentScope, onScopeDispose, shallowRef } from "vue";
import { aimStep, initialAimState, type AimOptions } from "@/input/gaze/aimSteering";
import type { GazeSample, GazeSource } from "@/input/gaze/gazeSource";
import { PointFilter, type OneEuroOptions } from "@/input/gaze/oneEuro";
import type { ControlIntent } from "@/input/intents";
import { DEFAULT_HEAD_SIZE } from "@/render/pixelRenderer";
import { headCenter, stageToWorld } from "@/render/stageGeometry";
import type { GamePoint, RoomDimensions } from "@/types/game";

export type GazeControlsStatus = "off" | "starting" | "tracking" | "error";

/** Three snake segments: aim points closer than this to the head do not steer. */
export const DEFAULT_DEAD_RADIUS = 45;

export interface UseGazeControlsOptions {
  source: GazeSource;
  /** Top-left corner of the own snake's head box in world units, or null while there is no snake. */
  getHead: () => GamePoint | null;
  getRoom: () => RoomDimensions | null;
  onIntent: (intent: ControlIntent) => void;
  /** Return false to hold steering back, for example while the keyboard is steering. */
  canSteer?: (now: number) => boolean;
  /** Samples are ignored while this returns false. */
  enabled?: () => boolean;
  headSize?: number;
  aim?: Partial<AimOptions>;
  filter?: OneEuroOptions;
  now?: () => number;
}

/**
 * Turns aim samples (pointer now, camera gaze later) into `steer` intents: smooth the point, aim from the head,
 * respect the dead zone, and report a new heading only when it changed.
 */
export function useGazeControls(options: UseGazeControlsOptions) {
  const { source } = options;
  const now = options.now ?? Date.now;
  const aimOptions: AimOptions = { deadRadius: DEFAULT_DEAD_RADIUS, ...options.aim };
  const filter = new PointFilter(options.filter);

  const status = shallowRef<GazeControlsStatus>("off");
  const error = shallowRef<string | null>(null);
  let aimState = initialAimState;
  let unsubscribe: (() => void) | null = null;

  function onSample(sample: GazeSample) {
    if (!(options.enabled?.() ?? true)) return;
    const head = options.getHead();
    const room = options.getRoom();
    if (!head || !room) return;

    const target = filter.filter(stageToWorld(sample, room), sample.t);
    const result = aimStep(aimState, headCenter(head, options.headSize ?? DEFAULT_HEAD_SIZE), target, aimOptions);
    if (result.steer === null) {
      aimState = result.state;
      return;
    }
    // Held back: keep the old state so the same heading is reported as soon as steering is allowed again.
    if (!(options.canSteer?.(now()) ?? true)) return;
    aimState = result.state;
    options.onIntent({ type: "steer", angle: result.steer });
  }

  async function start() {
    if (unsubscribe || status.value === "starting") return;
    status.value = "starting";
    error.value = null;
    filter.reset();
    aimState = initialAimState;
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
  }

  if (getCurrentScope()) onScopeDispose(stop);

  return { status, error, start, stop };
}
