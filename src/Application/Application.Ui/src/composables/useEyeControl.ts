import { getCurrentScope, onScopeDispose, shallowRef } from "vue";
import {
  CALIBRATION_TARGETS,
  CalibrationRecorder,
  defaultCalibration,
  type CalibrationRecorderOptions,
  type CalibrationTarget,
} from "@/input/gaze/calibration";
import type { CalibrationStorage } from "@/input/gaze/calibrationStorage";
import { classifyCameraError, type CameraErrorKind } from "@/input/gaze/cameraErrors";
import type { EyeActionsOptions } from "@/input/gaze/eyeActions";
import type { GazeSample, GazeSource } from "@/input/gaze/gazeSource";
import type { CameraGazeSource } from "@/input/gaze/mediapipeGazeSource";
import type { ControlIntent } from "@/input/intents";
import type { RoomDimensions } from "@/types/game";
import { useEyeActions } from "./useEyeActions";
import { useGazeControls } from "./useGazeControls";

export type EyeControlPhase = "off" | "starting" | "calibrating" | "active" | "error";

export interface UseEyeControlOptions extends EyeActionsOptions {
  source: CameraGazeSource;
  storage: CalibrationStorage;
  getRoom: () => RoomDimensions | null;
  onIntent: (intent: ControlIntent) => void;
  /** Return false to hold steering back, for example while the keyboard is steering. */
  canSteer?: (now: number) => boolean;
  calibration?: CalibrationRecorderOptions;
}

/** Calibrating shows each target for about a second and a half: half a second to get there, one to measure. */
const DEFAULT_CALIBRATION: CalibrationRecorderOptions = { framesPerTarget: 30, settleFrames: 15 };

/** Lets several consumers listen to one camera while this composable alone starts and stops it. */
function sharedSource(source: GazeSource): GazeSource {
  return { start: async () => undefined, stop: () => undefined, subscribe: (listener) => source.subscribe(listener) };
}

/**
 * Controlling the snake with the eyes: start the camera, calibrate once (and remember it), then steer from the
 * middle of the stage, shoot with a long blink and boost by looking far away.
 */
export function useEyeControl(options: UseEyeControlOptions) {
  const { source, storage } = options;
  const recorder = new CalibrationRecorder(options.calibration ?? DEFAULT_CALIBRATION);

  const phase = shallowRef<EyeControlPhase>("off");
  const errorKind = shallowRef<CameraErrorKind | null>(null);
  const errorMessage = shallowRef<string | null>(null);
  const calibrationTarget = shallowRef<CalibrationTarget | null>(null);
  const calibrationProgress = shallowRef(0);
  const calibrationFailed = shallowRef(false);

  let attempt = 0;
  let stopCalibrationListener: (() => void) | null = null;
  const active = () => phase.value === "active";
  const shared = sharedSource(source);

  const steering = useGazeControls({
    source: shared,
    origin: "center",
    getRoom: options.getRoom,
    onIntent: options.onIntent,
    canSteer: options.canSteer,
    enabled: active,
  });
  const actions = useEyeActions({ ...options, source: shared, enabled: active });

  function showCalibrationProgress() {
    const id = recorder.target;
    calibrationTarget.value = id ? (CALIBRATION_TARGETS.find((target) => target.id === id) ?? null) : null;
    calibrationProgress.value = recorder.progress;
  }

  function beginCalibration() {
    recorder.reset();
    phase.value = "calibrating";
    showCalibrationProgress();
  }

  function finishCalibration() {
    const result = recorder.result();
    if (!result) {
      calibrationFailed.value = true;
      beginCalibration();
      return;
    }
    storage.save(result);
    source.setCalibration(result);
    calibrationFailed.value = false;
    phase.value = "active";
    showCalibrationProgress();
  }

  function onCalibrationSample(sample: GazeSample) {
    if (phase.value !== "calibrating" || sample.present === false || !sample.raw) return;
    recorder.add(sample.raw);
    showCalibrationProgress();
    if (recorder.complete) finishCalibration();
  }

  async function enable() {
    if (phase.value !== "off" && phase.value !== "error") return;
    const myAttempt = ++attempt;
    phase.value = "starting";
    errorKind.value = null;
    errorMessage.value = null;
    calibrationFailed.value = false;

    try {
      await source.start();
    } catch (failure) {
      if (myAttempt !== attempt) return;
      source.stop();
      phase.value = "error";
      errorKind.value = classifyCameraError(failure);
      errorMessage.value = failure instanceof Error ? failure.message : String(failure);
      return;
    }
    if (myAttempt !== attempt) {
      source.stop();
      return;
    }

    await Promise.all([steering.start(), actions.start()]);
    stopCalibrationListener = source.subscribe(onCalibrationSample);
    const saved = storage.load();
    if (!saved) return beginCalibration();
    source.setCalibration(saved);
    phase.value = "active";
  }

  function disable() {
    attempt++;
    stopCalibrationListener?.();
    stopCalibrationListener = null;
    steering.stop();
    actions.stop();
    source.stop();
    phase.value = "off";
    calibrationTarget.value = null;
    calibrationProgress.value = 0;
  }

  /** Forgets the saved calibration and measures again. Only while the camera runs. */
  function recalibrate() {
    if (phase.value !== "active" && phase.value !== "calibrating") return;
    storage.clear();
    source.setCalibration(defaultCalibration);
    calibrationFailed.value = false;
    beginCalibration();
  }

  if (getCurrentScope()) onScopeDispose(disable);

  return {
    phase,
    errorKind,
    errorMessage,
    tracking: actions.tracking,
    calibrationTarget,
    calibrationProgress,
    calibrationFailed,
    enable,
    disable,
    recalibrate,
  };
}
