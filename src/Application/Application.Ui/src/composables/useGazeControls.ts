import { aimStep, initialAimState, type AimOptions } from "@/input/gaze/aimSteering";
import type { GazeSample, GazeSource } from "@/input/gaze/gazeSource";
import { PointFilter, type OneEuroOptions } from "@/input/gaze/oneEuro";
import type { ControlIntent } from "@/input/intents";
import { DEFAULT_HEAD_SIZE } from "@/render/pixelRenderer";
import { headCenter, stageToWorld } from "@/render/stageGeometry";
import type { GamePoint, RoomDimensions } from "@/types/game";
import { useGazeSubscription, type GazeStatus } from "./useGazeSubscription";

export type GazeControlsStatus = GazeStatus;

/** Three snake segments: aim points closer than this to the head do not steer. */
export const DEFAULT_DEAD_RADIUS = 45;

/** In joystick mode the dead zone around the middle of the stage is this fraction of the smaller side of the room. */
export const JOYSTICK_DEAD_FRACTION = 0.12;

export interface UseGazeControlsOptions {
  source: GazeSource;
  /**
   * `head`: aim from the own snake's head, like a mouse. `center`: aim from the middle of the stage, like a
   * joystick, which suits a camera that only knows roughly where the eyes point.
   */
  origin?: "head" | "center";
  /** Dead radius as a fraction of the smaller side of the room. An explicit `aim.deadRadius` wins. */
  deadFraction?: number;
  /** Top-left corner of the own snake's head box in world units, or null while there is no snake. Used in `head` mode. */
  getHead?: () => GamePoint | null;
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
 * Turns aim samples (pointer or camera gaze) into `steer` intents: smooth the point, aim from the head or the
 * middle of the stage, respect the dead zone, and report a new heading only when it changed.
 */
export function useGazeControls(options: UseGazeControlsOptions) {
  const now = options.now ?? Date.now;
  const origin = options.origin ?? "head";
  const filter = new PointFilter(options.filter);
  let aimState = initialAimState;

  const forgetAim = () => {
    filter.reset();
    aimState = initialAimState;
  };

  function aimFrom(room: RoomDimensions): GamePoint | null {
    if (origin === "center") return { x: room.width / 2, y: room.height / 2 };
    const head = options.getHead?.() ?? null;
    return head ? headCenter(head, options.headSize ?? DEFAULT_HEAD_SIZE) : null;
  }

  function aimOptionsFor(room: RoomDimensions): AimOptions {
    const fraction = options.deadFraction ?? (origin === "center" ? JOYSTICK_DEAD_FRACTION : undefined);
    const fallbackRadius = fraction === undefined ? DEFAULT_DEAD_RADIUS : fraction * Math.min(room.width, room.height);
    return { ...options.aim, deadRadius: options.aim?.deadRadius ?? fallbackRadius };
  }

  function onSample(sample: GazeSample) {
    if (!(options.enabled?.() ?? true)) return;
    if (sample.present === false) {
      forgetAim();
      return;
    }
    const room = options.getRoom();
    const from = room ? aimFrom(room) : null;
    if (!room || !from) return;

    const target = filter.filter(stageToWorld(sample, room), sample.t);
    const result = aimStep(aimState, from, target, aimOptionsFor(room), sample.t);
    if (result.steer === null) {
      aimState = result.state;
      return;
    }
    // Held back: keep the old state so the same heading is reported as soon as steering is allowed again.
    if (!(options.canSteer?.(now()) ?? true)) return;
    aimState = result.state;
    options.onIntent({ type: "steer", angle: result.steer });
  }

  return useGazeSubscription(options.source, onSample, { onStart: forgetAim });
}
