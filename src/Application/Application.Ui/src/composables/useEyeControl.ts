import { computed, getCurrentScope, onScopeDispose, shallowRef } from "vue";
import {
  CALIBRATION_TARGETS,
  CalibrationRecorder,
  defaultCalibration,
  type CalibrationRecorderOptions,
  type CalibrationTarget,
} from "@/input/gaze/calibration";
import type { CalibrationStorage } from "@/input/gaze/calibrationStorage";
import type { CameraDevice } from "@/input/gaze/cameraDevices";
import { classifyCameraError, type CameraErrorKind } from "@/input/gaze/cameraErrors";
import type { CameraPreferenceStorage } from "@/input/gaze/cameraPreference";
import type { EyeActionsOptions } from "@/input/gaze/eyeActions";
import type { GazeSample, GazeSource } from "@/input/gaze/gazeSource";
import type { AimOptions } from "@/input/gaze/aimSteering";
import type { CameraGazeSource } from "@/input/gaze/mediapipeGazeSource";
import type { ControlIntent } from "@/input/intents";
import type { GamePoint, RoomDimensions } from "@/types/game";
import { useEyeActions } from "./useEyeActions";
import { useGazeControls } from "./useGazeControls";

export type EyeControlPhase = "off" | "starting" | "calibrating" | "active" | "error";

export interface UseEyeControlOptions extends EyeActionsOptions {
  source: CameraGazeSource;
  storage: CalibrationStorage;
  /** Remembers which camera the player picked. Without it the browser default is used every time. */
  cameraStorage?: CameraPreferenceStorage;
  getRoom: () => RoomDimensions | null;
  /**
   * Top-left corner of the own snake's head box. With it the snake steers toward where the player looks, so looking at
   * the snake itself changes nothing. Without it (the start page) steering aims from the middle of the stage.
   */
  getHead?: () => GamePoint | null;
  /** Tuning for steering. The defaults wait 0.3 s before the first turn. */
  aim?: Partial<AimOptions>;
  onIntent: (intent: ControlIntent) => void;
  /** Return false to hold steering back, for example while the keyboard is steering. */
  canSteer?: (now: number) => boolean;
  calibration?: CalibrationRecorderOptions;
}

/** Calibrating shows each target for about a second and a half: half a second to get there, one to measure. */
const DEFAULT_CALIBRATION: CalibrationRecorderOptions = { framesPerTarget: 30, settleFrames: 15 };

/**
 * A webcam knows roughly where the eyes point, not exactly: the dead zone around the snake's head is a sixth of the room,
 * and a turn needs the gaze to stay put for a moment so glancing at something does not steer.
 */
const EYE_DEAD_FRACTION = 0.15;
const EYE_DWELL_MS = 300;

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
  const cameraStorage: CameraPreferenceStorage = options.cameraStorage ?? { load: () => null, save: () => undefined };
  const recorder = new CalibrationRecorder(options.calibration ?? DEFAULT_CALIBRATION);

  const phase = shallowRef<EyeControlPhase>("off");
  const errorKind = shallowRef<CameraErrorKind | null>(null);
  const errorMessage = shallowRef<string | null>(null);
  const calibrationTarget = shallowRef<CalibrationTarget | null>(null);
  const calibrationProgress = shallowRef(0);
  const calibrationFailed = shallowRef(false);
  const cameras = shallowRef<CameraDevice[]>([]);
  const activeCamera = shallowRef<CameraDevice | null>(null);
  /** The camera in use looks like software (OBS and the like) while a real camera is available. */
  const virtualCameraWarning = computed(() => activeCamera.value?.virtual === true && cameras.value.some((camera) => !camera.virtual));

  let attempt = 0;
  let stopCalibrationListener: (() => void) | null = null;
  const active = () => phase.value === "active";
  const shared = sharedSource(source);

  const steering = useGazeControls({
    source: shared,
    origin: options.getHead ? "head" : "center",
    getHead: options.getHead,
    deadFraction: EYE_DEAD_FRACTION,
    aim: { dwellMs: EYE_DWELL_MS, ...options.aim },
    getRoom: options.getRoom,
    onIntent: options.onIntent,
    canSteer: options.canSteer,
    enabled: active,
  });
  const actions = useEyeActions({ ...options, source: shared, enabled: active });

  async function refreshCameras() {
    cameras.value = await source.listCameras();
    activeCamera.value = source.getActiveCamera();
  }

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

    source.setPreferredCamera(cameraStorage.load());
    try {
      await source.start();
    } catch (failure) {
      if (myAttempt !== attempt) return;
      source.stop();
      fail(failure);
      return;
    }
    if (myAttempt !== attempt) {
      source.stop();
      return;
    }

    await Promise.all([steering.start(), actions.start()]);
    stopCalibrationListener = source.subscribe(onCalibrationSample);
    await refreshCameras();
    const saved = storage.load();
    if (!saved) return beginCalibration();
    source.setCalibration(saved);
    phase.value = "active";
  }

  function fail(failure: unknown) {
    phase.value = "error";
    errorKind.value = classifyCameraError(failure);
    errorMessage.value = failure instanceof Error ? failure.message : String(failure);
  }

  /** Stops listening and shuts the camera. The phase is the caller's to set. */
  function teardown() {
    attempt++;
    stopCalibrationListener?.();
    stopCalibrationListener = null;
    steering.stop();
    actions.stop();
    source.stop();
    calibrationTarget.value = null;
    calibrationProgress.value = 0;
  }

  function disable() {
    teardown();
    phase.value = "off";
    activeCamera.value = null;
  }

  /**
   * Uses another camera and remembers the choice. While the camera is on it is reopened and listeners and calibration stay;
   * if the new camera cannot be opened everything stops with an error, and enabling again retries.
   */
  async function selectCamera(cameraId: string) {
    cameraStorage.save(cameraId);
    source.setPreferredCamera(cameraId);
    if (phase.value !== "active" && phase.value !== "calibrating") return;
    try {
      await source.selectCamera(cameraId);
    } catch (failure) {
      teardown();
      fail(failure);
      return;
    }
    await refreshCameras();
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
    cameras,
    activeCamera,
    virtualCameraWarning,
    enable,
    disable,
    recalibrate,
    selectCamera,
  };
}
