import { missingEyeBlendshapes, type BlendshapeScore } from "./blendshapes";
import type { Calibration } from "./calibration";
import { isLikelyVirtualCamera, toCameraDevices, type CameraDevice, type MediaDeviceInfoLike } from "./cameraDevices";
import { cameraSize, detectIntervalMs, smoothDetectTime } from "./detectBudget";
import { reportDetectTiming } from "./detectTimings";
import { CameraError, classifyCameraError, isCameraSupported, type NavigatorLike } from "./cameraErrors";
import { EyeFrameProcessor } from "./eyeFrameProcessor";
import { missingFaceGestureBlendshapes } from "./faceGestures";
import { loadFaceLandmarker } from "./faceLandmarker";
import type { GazeSample, GazeSource } from "./gazeSource";
import { HeadFrameProcessor } from "./headFrameProcessor";
import type { HeadMatrix } from "./headPose";

/** What the camera reads: the eyes (where they look, blinks) or the head (where it points, mouth, eyebrows). */
export type FaceMode = "eyes" | "head";

/** The part of a MediaPipe `FaceLandmarkerResult` this app reads. */
export interface LandmarkerResultLike {
  faceLandmarks?: unknown[];
  faceBlendshapes?: Array<{ categories: BlendshapeScore[] }>;
  facialTransformationMatrixes?: HeadMatrix[];
}

export interface LandmarkerLike {
  /** The result now, or later when the model runs on another thread. */
  detectForVideo(video: VideoLike, timestampMs: number): LandmarkerResultLike | Promise<LandmarkerResultLike>;
  /** Turns on or off the parts of the result that cost something and are not always read (the head's transformation matrix). */
  setOutputs?(outputs: { matrices: boolean }): void;
  close(): void;
}

/** The part of an `HTMLVideoElement` the source uses. */
export interface VideoLike {
  readyState: number;
  currentTime: number;
  videoWidth?: number;
  videoHeight?: number;
  srcObject: unknown;
  muted: boolean;
  playsInline: boolean;
  play(): Promise<unknown>;
  pause(): void;
  remove?(): void;
  /** Runs the callback once per decoded video frame, unlike animation frames which run at the display rate. */
  requestVideoFrameCallback?(callback: () => void): number;
  cancelVideoFrameCallback?(handle: number): void;
}

export interface CameraStreamLike {
  getTracks(): Array<{ stop(): void }>;
  getVideoTracks?(): Array<{ label: string; getSettings?(): { deviceId?: string } }>;
}

/** What the camera pipeline has done so far, to see where it stops when no face is found. */
export interface GazeDiagnostics {
  /** Frames handed to the model. */
  frames: number;
  /** Of those, frames where the model found a face (landmarks). */
  withLandmarks: number;
  /** Of those, frames with a usable blendshape result, which is what the gaze needs. */
  withFace: number;
  /** Faces whose result lacked some eye blendshape, so no gaze could be read. */
  incomplete: number;
  /** The eye blendshapes missing from the latest incomplete face. */
  missing: string[];
  /** Loop runs without a model call: the video had no data or no new frame, or the page was hidden. */
  skipped: number;
  /** Frames left out on purpose because detection is slow and the game needs the time. */
  throttled: number;
  /** How long a detection takes (running average, and the longest since start), in ms. */
  detectMs: { avg: number; max: number };
  videoWidth: number;
  videoHeight: number;
  readyState: number;
  /** Average brightness of the latest measured frame, 0 (black) to 1, or null when it could not be measured. */
  brightness: number | null;
}

export interface CameraGazeSource extends GazeSource {
  /** Calibration for the mode in use. */
  setCalibration(calibration: Calibration): void;
  /** What the samples describe. Eyes by default. The calibration of the other mode is kept. */
  setMode(mode: FaceMode): void;
  getDiagnostics(): GazeDiagnostics;
  /** Which camera to open on the next start. Null is the browser default. */
  setPreferredCamera(cameraId: string | null): void;
  /** The video cameras, real ones first. Names appear once the camera permission was given. */
  listCameras(): Promise<CameraDevice[]>;
  /** The camera in use, or null while stopped. */
  getActiveCamera(): CameraDevice | null;
  /** Switches to that camera. While running the stream is reopened, listeners and calibration stay. */
  selectCamera(cameraId: string): Promise<void>;
}

export interface MediaPipeGazeSourceOptions {
  processor?: EyeFrameProcessor;
  headProcessor?: HeadFrameProcessor;
  /** Mode to start in. */
  mode?: FaceMode;
  navigatorLike?: NavigatorLike | null;
  /** Opens a camera by device id, or the browser default for null, with the picture size the mode needs. */
  getStream?: (cameraId: string | null, mode?: FaceMode) => Promise<CameraStreamLike>;
  enumerateDevices?: () => Promise<MediaDeviceInfoLike[]>;
  /** Camera to open first. */
  cameraId?: string | null;
  loadLandmarker?: () => Promise<LandmarkerLike>;
  createVideo?: () => VideoLike;
  requestFrame?: (callback: () => void) => number;
  cancelFrame?: (handle: number) => void;
  now?: () => number;
  /** Called when the model throws on a frame. The frame then counts as one without a face. Logs once per message by default. */
  onDetectError?: (error: unknown) => void;
  /** Receives a one line summary of the diagnostics every two seconds while running. Silent by default; pass `console.info` to debug the camera. */
  log?: (summary: string) => void;
  /** True while the page is hidden: nothing is detected then. Defaults to `document.hidden`. */
  isHidden?: () => boolean;
  /** Told how long each detection took, and when it ended. Defaults to the frame meter's feed. */
  onDetectTiming?: (atMs: number, durationMs: number) => void;
  /** Average brightness of the video, 0 to 1, or null. Called once per summary. Defaults to a 16 by 12 canvas sample. */
  measureBrightness?: (video: VideoLike) => number | null;
}

/** Both a video frame has to be decoded (HAVE_CURRENT_DATA) before the model can look at it. */
const HAVE_CURRENT_DATA = 2;
const LOG_EVERY_MS = 2000;
/** A frame may come a little before the interval is over: cameras do not deliver on the dot. */
const DUE_SHARE = 0.75;
/** After this many frames without a face the summary says so. */
const NO_FACE_HINT_FRAMES = 150;

const defaultGetStream = (cameraId: string | null, mode: FaceMode = "eyes") => {
  const size = { width: { ideal: cameraSize(mode).width }, height: { ideal: cameraSize(mode).height } };
  return navigator.mediaDevices.getUserMedia({
    video: cameraId ? { deviceId: { exact: cameraId }, ...size } : { ...size, facingMode: "user" },
    audio: false,
  });
};

const defaultEnumerateDevices = () => navigator.mediaDevices.enumerateDevices();

const emptyCounters = () => ({ frames: 0, withLandmarks: 0, withFace: 0, incomplete: 0, skipped: 0, throttled: 0, missing: [] as string[] });

function createWarnOnce() {
  const warned = new Set<string>();
  return (error: unknown) => {
    const message = error instanceof Error ? error.message : String(error);
    if (warned.has(message)) return;
    warned.add(message);
    console.warn("[eye] face detection failed:", error);
  };
}

/**
 * A small mirrored preview in the corner, like every video call. It shows the player what the camera sees and that
 * the camera is on. Only the preview is mirrored: the model reads the raw frame, because MediaPipe names the left and
 * right blendshapes from the subject's point of view.
 */
function defaultCreateVideo(): VideoLike {
  const element = document.createElement("video");
  element.setAttribute("aria-label", "Camera preview");
  Object.assign(element.style, {
    position: "fixed",
    left: "8px",
    bottom: "140px",
    width: "160px",
    height: "120px",
    objectFit: "cover",
    transform: "scaleX(-1)",
    background: "#000000",
    border: "2px solid rgba(255, 255, 255, 0.5)",
    zIndex: "15",
    pointerEvents: "none",
  });
  document.body.appendChild(element);
  return element;
}

/** Average brightness of a tiny copy of the frame. A camera that delivers black frames gives about 0. */
function measureVideoBrightness(video: VideoLike): number | null {
  try {
    const canvas = document.createElement("canvas");
    canvas.width = 16;
    canvas.height = 12;
    const context = canvas.getContext("2d", { willReadFrequently: true });
    if (!context) return null;
    context.drawImage(video as unknown as HTMLVideoElement, 0, 0, 16, 12);
    const { data } = context.getImageData(0, 0, 16, 12);
    let sum = 0;
    for (let index = 0; index < data.length; index += 4) sum += 0.2126 * data[index] + 0.7152 * data[index + 1] + 0.0722 * data[index + 2];
    return Math.round((sum / (data.length / 4) / 255) * 100) / 100;
  } catch {
    return null;
  }
}

/**
 * Gaze from the webcam: MediaPipe Face Landmarker reads the eye blendshapes of every video frame and the frame
 * processor turns them into calibrated gaze samples. Frames are processed in the browser and never leave it.
 */
export function createMediaPipeGazeSource(options: MediaPipeGazeSourceOptions = {}): CameraGazeSource {
  const eyeProcessor = options.processor ?? new EyeFrameProcessor();
  const headProcessor = options.headProcessor ?? new HeadFrameProcessor();
  let mode: FaceMode = options.mode ?? "eyes";
  const navigatorLike = options.navigatorLike === undefined ? (typeof navigator === "undefined" ? null : navigator) : options.navigatorLike;
  const getStream = options.getStream ?? defaultGetStream;
  const enumerateDevices = options.enumerateDevices ?? defaultEnumerateDevices;
  const loadLandmarker = options.loadLandmarker ?? (() => loadFaceLandmarker());
  const createVideo = options.createVideo ?? defaultCreateVideo;
  const requestFrame = options.requestFrame ?? ((callback: () => void) => requestAnimationFrame(callback));
  const cancelFrame = options.cancelFrame ?? ((handle: number) => cancelAnimationFrame(handle));
  const now = options.now ?? (() => performance.now());
  const onDetectError = options.onDetectError ?? createWarnOnce();
  const log = options.log ?? (() => undefined);
  const measureBrightness = options.measureBrightness ?? measureVideoBrightness;
  const isHidden = options.isHidden ?? (() => typeof document !== "undefined" && document.hidden);
  const reportTiming = options.onDetectTiming ?? reportDetectTiming;

  const listeners = new Set<(sample: GazeSample) => void>();
  let running = false;
  let starting: Promise<void> | null = null;
  let generation = 0;
  let stream: CameraStreamLike | null = null;
  let preferredCameraId: string | null = options.cameraId ?? null;
  let activeCamera: CameraDevice | null = null;
  let landmarker: LandmarkerLike | null = null;
  let video: VideoLike | null = null;
  let frameHandle: number | null = null;
  let frameFromVideo = false;
  let lastVideoTime = -1;
  let lastLogAt = 0;
  let brightness: number | null = null;
  let counters = emptyCounters();
  // What detection costs, so it can leave the game the time it needs.
  let lastDetectAt = Number.NEGATIVE_INFINITY;
  let averageDetectMs = 0;
  let longestDetectMs = 0;
  /** A frame is with the model (on another thread): the next is not sent until it comes back. */
  let inFlight = false;

  const emit = (sample: GazeSample) => listeners.forEach((listener) => listener(sample));

  function scheduleFrame() {
    if (video?.requestVideoFrameCallback) {
      frameFromVideo = true;
      frameHandle = video.requestVideoFrameCallback(tick);
      return;
    }
    frameFromVideo = false;
    frameHandle = requestFrame(tick);
  }

  function cancelScheduledFrame() {
    if (frameHandle === null) return;
    if (frameFromVideo) video?.cancelVideoFrameCallback?.(frameHandle);
    else cancelFrame(frameHandle);
    frameHandle = null;
  }

  function releaseResources() {
    cancelScheduledFrame();
    stream?.getTracks().forEach((track) => track.stop());
    landmarker?.close();
    video?.pause();
    if (video) video.srcObject = null;
    video?.remove?.();
    stream = null;
    activeCamera = null;
    landmarker = null;
    video = null;
  }

  function describeCamera(openedStream: CameraStreamLike): CameraDevice | null {
    const track = openedStream.getVideoTracks?.()[0];
    if (!track) return null;
    return { id: track.getSettings?.().deviceId ?? preferredCameraId ?? "", label: track.label, virtual: isLikelyVirtualCamera(track.label) };
  }

  /** The preferred camera, or the default when that one is gone. Other failures are the player's to fix. */
  async function acquireStream(): Promise<CameraStreamLike> {
    try {
      return await getStream(preferredCameraId, mode);
    } catch (failure) {
      const kind = classifyCameraError(failure);
      if (preferredCameraId === null || kind !== "no-camera") throw new CameraError(kind, "The camera could not be opened.", failure);
      preferredCameraId = null;
      try {
        return await getStream(null, mode);
      } catch (second) {
        throw new CameraError(classifyCameraError(second), "The camera could not be opened.", second);
      }
    }
  }

  /** A failed detection is one frame without a face. The next frame may work again. */
  function detect(currentVideo: VideoLike, currentLandmarker: LandmarkerLike, t: number): LandmarkerResultLike | null | Promise<LandmarkerResultLike | null> {
    try {
      const outcome = currentLandmarker.detectForVideo(currentVideo, t);
      if (!(outcome instanceof Promise)) return outcome;
      return outcome.catch((failure: unknown) => {
        onDetectError(failure);
        return null;
      });
    } catch (failure) {
      onDetectError(failure);
      return null;
    }
  }

  function diagnostics(): GazeDiagnostics {
    return {
      ...counters,
      missing: [...counters.missing],
      videoWidth: video?.videoWidth ?? 0,
      videoHeight: video?.videoHeight ?? 0,
      readyState: video?.readyState ?? 0,
      brightness,
      detectMs: { avg: averageDetectMs, max: longestDetectMs },
    };
  }

  function summary() {
    const d = diagnostics();
    const hint = d.frames >= NO_FACE_HINT_FRAMES && d.withLandmarks === 0 ? " hint=no face found: check the preview and the light" : "";
    return (
      `[eye] frames=${d.frames} landmarks=${d.withLandmarks} withFace=${d.withFace} incomplete=${d.incomplete} skipped=${d.skipped} ` +
      `missing=${d.missing.join(",") || "-"} video=${d.videoWidth}x${d.videoHeight} ready=${d.readyState} ` +
      `brightness=${d.brightness ?? "n/a"}${hint}`
    );
  }

  function count(result: LandmarkerResultLike | null, categories: BlendshapeScore[] | null) {
    counters.frames++;
    if ((result?.faceLandmarks?.length ?? 0) > 0) counters.withLandmarks++;
    if (!categories) return;
    counters.withFace++;
    const missing = mode === "head" ? missingFaceGestureBlendshapes(categories) : missingEyeBlendshapes(categories);
    if (missing.length === 0) return;
    counters.incomplete++;
    counters.missing = missing;
  }

  /** Detection costs the game frames, so it is not run again before a multiple of what it costs has passed. */
  function dueForDetection(t: number): boolean {
    if (t - lastDetectAt >= detectIntervalMs(averageDetectMs) * DUE_SHARE) return true;
    counters.throttled++;
    return false;
  }

  function processFrame(currentVideo: VideoLike, currentLandmarker: LandmarkerLike) {
    if (isHidden() || currentVideo.readyState < HAVE_CURRENT_DATA || currentVideo.currentTime === lastVideoTime) {
      counters.skipped++;
      return;
    }
    const t = now();
    if (inFlight) {
      counters.throttled++;
      return;
    }
    if (!dueForDetection(t)) return;
    lastVideoTime = currentVideo.currentTime;
    lastDetectAt = t;
    const outcome = detect(currentVideo, currentLandmarker, t);
    if (outcome instanceof Promise) {
      inFlight = true;
      const myGeneration = generation;
      void outcome.then((result) => {
        inFlight = false;
        if (running && myGeneration === generation) finishFrame(t, result);
      });
      return;
    }
    finishFrame(t, outcome);
  }

  /** What is done with a frame's result, whether it came at once or from the worker. */
  function finishFrame(t: number, result: LandmarkerResultLike | null) {
    const took = now() - t;
    averageDetectMs = smoothDetectTime(averageDetectMs, took);
    longestDetectMs = Math.max(longestDetectMs, took);
    reportTiming(t + took, took);
    const categories = result?.faceBlendshapes?.[0]?.categories ?? null;
    count(result, categories);
    emit(
      mode === "head"
        ? headProcessor.process({ t, blendshapes: categories, matrix: result?.facialTransformationMatrixes?.[0] ?? null })
        : eyeProcessor.process({ t, blendshapes: categories })
    );
  }

  function tick() {
    if (!running || !video || !landmarker) return;
    scheduleFrame();
    processFrame(video, landmarker);
    if (now() - lastLogAt < LOG_EVERY_MS) return;
    lastLogAt = now();
    brightness = measureBrightness(video);
    log(summary());
  }

  async function open(myGeneration: number) {
    if (!isCameraSupported(navigatorLike)) {
      throw new CameraError("unsupported", "This browser cannot use the camera here (it needs https or localhost).");
    }
    const cancelled = () => myGeneration !== generation;

    const openedStream = await acquireStream();
    stream = openedStream;
    activeCamera = describeCamera(openedStream);
    if (cancelled()) return releaseResources();

    try {
      landmarker = await loadLandmarker();
      landmarker.setOutputs?.({ matrices: mode === "head" });
    } catch (failure) {
      releaseResources();
      throw new CameraError("model", "The face tracking model could not be loaded.", failure);
    }
    if (cancelled()) return releaseResources();

    const element = createVideo();
    element.muted = true;
    element.playsInline = true;
    element.srcObject = openedStream;
    video = element;
    try {
      await element.play();
    } catch (failure) {
      releaseResources();
      throw new CameraError(classifyCameraError(failure), "The camera video could not be started.", failure);
    }
    if (cancelled()) return releaseResources();

    running = true;
    lastVideoTime = -1;
    counters = emptyCounters();
    lastDetectAt = Number.NEGATIVE_INFINITY;
    averageDetectMs = 0;
    longestDetectMs = 0;
    brightness = null;
    lastLogAt = now();
    scheduleFrame();
  }

  async function startSource() {
    if (running) return;
    starting ??= open(generation).finally(() => {
      starting = null;
    });
    await starting;
  }

  function stopSource() {
    generation++;
    running = false;
    inFlight = false;
    releaseResources();
  }

  return {
    start: startSource,
    stop: stopSource,

    subscribe(listener) {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },

    setCalibration(calibration) {
      (mode === "head" ? headProcessor : eyeProcessor).setCalibration(calibration);
    },

    setMode(next) {
      mode = next;
      landmarker?.setOutputs?.({ matrices: next === "head" });
    },

    getDiagnostics: diagnostics,

    setPreferredCamera(cameraId) {
      preferredCameraId = cameraId;
    },

    async listCameras() {
      try {
        return toCameraDevices(await enumerateDevices());
      } catch {
        return [];
      }
    },

    getActiveCamera: () => (activeCamera ? { ...activeCamera } : null),

    async selectCamera(cameraId) {
      preferredCameraId = cameraId;
      if (!running && !starting) return;
      stopSource();
      await startSource();
    },
  };
}
