import { computed, getCurrentScope, onScopeDispose, shallowRef } from "vue";
import {
  CALIBRATION_TARGETS,
  CalibrationRecorder,
  defaultCalibration,
  type Calibration,
  type CalibrationRecorderOptions,
  type CalibrationTarget,
} from "@/input/gaze/calibration";
import type { CalibrationStorage } from "@/input/gaze/calibrationStorage";
import type { CameraDevice } from "@/input/gaze/cameraDevices";
import { classifyCameraError, type CameraErrorKind } from "@/input/gaze/cameraErrors";
import type { CameraPreferenceStorage } from "@/input/gaze/cameraPreference";
import type { EyeActionsOptions } from "@/input/gaze/eyeActions";
import type { FaceModePreference } from "@/input/gaze/faceModePreference";
import { defaultHeadCalibration } from "@/input/gaze/headFrameProcessor";
import type { GazeSample, GazeSource } from "@/input/gaze/gazeSource";
import type { AimOptions } from "@/input/gaze/aimSteering";
import type { CameraGazeSource, FaceMode } from "@/input/gaze/mediapipeGazeSource";
import type { ControlIntent } from "@/input/intents";
import type { GamePoint, RoomDimensions } from "@/types/game";
import { useEyeActions } from "./useEyeActions";
import { useGazeControls } from "./useGazeControls";

export type EyeControlPhase = "off" | "starting" | "calibrating" | "active" | "error";

export interface UseEyeControlOptions extends EyeActionsOptions {
  source: CameraGazeSource;
  /** Eye calibration. */
  storage: CalibrationStorage;
  /** Head calibration. Without it the head calibration is not remembered. */
  headStorage?: CalibrationStorage;
  /** Remembers whether the player uses the eyes or the head. */
  modeStorage?: FaceModePreference;
  /** Mode to start in. The remembered mode, then eyes, when left out. */
  mode?: FaceMode;
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

/**
 * Head turns are deliberate and steady, so the head mode steers like a joystick from the middle of the stage with a
 * smaller dead zone and no waiting before the first turn.
 */
const HEAD_DEAD_FRACTION = 0.12;
const HEAD_DWELL_MS = 0;

const unsavedCalibration: CalibrationStorage = { load: () => null, save: () => undefined, clear: () => undefined };

/** Lets several consumers listen to one camera while this composable alone starts and stops it. */
function sharedSource(source: GazeSource): GazeSource {
  return { start: async () => undefined, stop: () => undefined, subscribe: (listener) => source.subscribe(listener) };
}

/**
 * Controlling the snake with the face. Eyes mode: steer toward where the player looks, shoot with a long blink and
 * boost by looking far away. Head mode: steer by turning the head, shoot by opening the mouth and boost by raising
 * the eyebrows. Each mode is calibrated once and remembered.
 */
export function useEyeControl(options: UseEyeControlOptions) {
  const { source } = options;
  const storages: Record<FaceMode, CalibrationStorage> = { eyes: options.storage, head: options.headStorage ?? unsavedCalibration };
  const defaults: Record<FaceMode, Calibration> = { eyes: defaultCalibration, head: defaultHeadCalibration };
  const modeStorage: FaceModePreference = options.modeStorage ?? { load: () => null, save: () => undefined };
  const cameraStorage: CameraPreferenceStorage = options.cameraStorage ?? { load: () => null, save: () => undefined };
  const recorder = new CalibrationRecorder(options.calibration ?? DEFAULT_CALIBRATION);

  const mode = shallowRef<FaceMode>(options.mode ?? modeStorage.load() ?? "eyes");
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

  const eyeSteering = useGazeControls({
    source: shared,
    origin: options.getHead ? "head" : "center",
    getHead: options.getHead,
    deadFraction: EYE_DEAD_FRACTION,
    aim: { dwellMs: EYE_DWELL_MS, ...options.aim },
    getRoom: options.getRoom,
    onIntent: options.onIntent,
    canSteer: options.canSteer,
    enabled: () => active() && mode.value === "eyes",
  });
  const headSteering = useGazeControls({
    source: shared,
    origin: "center",
    deadFraction: HEAD_DEAD_FRACTION,
    aim: { dwellMs: HEAD_DWELL_MS, ...options.aim },
    getRoom: options.getRoom,
    onIntent: options.onIntent,
    canSteer: options.canSteer,
    enabled: () => active() && mode.value === "head",
  });
  const actions = useEyeActions({ ...options, source: shared, enabled: active, mode: () => mode.value });

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
    storages[mode.value].save(result);
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

    source.setMode(mode.value);
    await Promise.all([eyeSteering.start(), headSteering.start(), actions.start()]);
    stopCalibrationListener = source.subscribe(onCalibrationSample);
    await refreshCameras();
    applySavedCalibration();
  }

  /** Active with the saved calibration of the mode in use, or calibrating when there is none. */
  function applySavedCalibration() {
    const saved = storages[mode.value].load();
    if (!saved) return beginCalibration();
    source.setCalibration(saved);
    calibrationFailed.value = false;
    recorder.reset();
    phase.value = "active";
    calibrationTarget.value = null;
    calibrationProgress.value = 0;
  }

  /** Controls with the eyes or the head. Remembered; while the camera runs the new mode is calibrated or loaded. */
  function setMode(next: FaceMode) {
    if (next === mode.value) return;
    mode.value = next;
    modeStorage.save(next);
    if (phase.value !== "active" && phase.value !== "calibrating") return;
    source.setMode(next);
    applySavedCalibration();
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
    eyeSteering.stop();
    headSteering.stop();
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
    storages[mode.value].clear();
    source.setCalibration(defaults[mode.value]);
    calibrationFailed.value = false;
    beginCalibration();
  }

  if (getCurrentScope()) onScopeDispose(disable);

  return {
    mode,
    setMode,
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
