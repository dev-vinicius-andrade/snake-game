import { missingEyeBlendshapes, type BlendshapeScore } from "./blendshapes";
import type { Calibration } from "./calibration";
import { CameraError, classifyCameraError, isCameraSupported, type NavigatorLike } from "./cameraErrors";
import { EyeFrameProcessor } from "./eyeFrameProcessor";
import { loadFaceLandmarker } from "./faceLandmarker";
import type { GazeSample, GazeSource } from "./gazeSource";

/** The part of a MediaPipe `FaceLandmarkerResult` this app reads. */
export interface LandmarkerResultLike {
  faceBlendshapes?: Array<{ categories: BlendshapeScore[] }>;
}

export interface LandmarkerLike {
  detectForVideo(video: VideoLike, timestampMs: number): LandmarkerResultLike;
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
}

export interface CameraStreamLike {
  getTracks(): Array<{ stop(): void }>;
}

/** What the camera pipeline has done so far, to see where it stops when no face is found. */
export interface GazeDiagnostics {
  /** Frames handed to the model. */
  frames: number;
  /** Of those, frames where it found a face. */
  withFace: number;
  /** Faces whose result lacked some eye blendshape, so no gaze could be read. */
  incomplete: number;
  /** The eye blendshapes missing from the latest incomplete face. */
  missing: string[];
  /** Loop runs without a model call: the video had no data or no new frame. */
  skipped: number;
  videoWidth: number;
  videoHeight: number;
  readyState: number;
}

export interface CameraGazeSource extends GazeSource {
  setCalibration(calibration: Calibration): void;
  getDiagnostics(): GazeDiagnostics;
}

export interface MediaPipeGazeSourceOptions {
  processor?: EyeFrameProcessor;
  navigatorLike?: NavigatorLike | null;
  getStream?: () => Promise<CameraStreamLike>;
  loadLandmarker?: () => Promise<LandmarkerLike>;
  createVideo?: () => VideoLike;
  requestFrame?: (callback: () => void) => number;
  cancelFrame?: (handle: number) => void;
  now?: () => number;
  /** Called when the model throws on a frame. The frame then counts as one without a face. Logs once per message by default. */
  onDetectError?: (error: unknown) => void;
  /** Receives a one line summary of the diagnostics every two seconds while running. Defaults to `console.info`. */
  log?: (summary: string) => void;
}

/** Both a video frame has to be decoded (HAVE_CURRENT_DATA) before the model can look at it. */
const HAVE_CURRENT_DATA = 2;

const defaultGetStream = () =>
  navigator.mediaDevices.getUserMedia({
    video: { width: { ideal: 640 }, height: { ideal: 480 }, facingMode: "user" },
    audio: false,
  });

const emptyCounters = () => ({ frames: 0, withFace: 0, incomplete: 0, skipped: 0, missing: [] as string[] });

function createWarnOnce() {
  const warned = new Set<string>();
  return (error: unknown) => {
    const message = error instanceof Error ? error.message : String(error);
    if (warned.has(message)) return;
    warned.add(message);
    console.warn("[eye] face detection failed:", error);
  };
}

/** Kept in the page, invisible: some browsers do not deliver frames to a video element that is not attached. */
function defaultCreateVideo(): VideoLike {
  const element = document.createElement("video");
  element.setAttribute("aria-hidden", "true");
  Object.assign(element.style, { position: "fixed", left: "0", top: "0", width: "1px", height: "1px", opacity: "0", pointerEvents: "none" });
  document.body.appendChild(element);
  return element;
}

const LOG_EVERY_MS = 2000;

/**
 * Gaze from the webcam: MediaPipe Face Landmarker reads the eye blendshapes of every video frame and the frame
 * processor turns them into calibrated gaze samples. Frames are processed in the browser and never leave it.
 * The video is not mirrored, because MediaPipe names the left and right blendshapes from the subject's view.
 */
export function createMediaPipeGazeSource(options: MediaPipeGazeSourceOptions = {}): CameraGazeSource {
  const processor = options.processor ?? new EyeFrameProcessor();
  const navigatorLike = options.navigatorLike === undefined ? (typeof navigator === "undefined" ? null : navigator) : options.navigatorLike;
  const getStream = options.getStream ?? defaultGetStream;
  const loadLandmarker = options.loadLandmarker ?? (() => loadFaceLandmarker());
  const createVideo = options.createVideo ?? defaultCreateVideo;
  const requestFrame = options.requestFrame ?? ((callback: () => void) => requestAnimationFrame(callback));
  const cancelFrame = options.cancelFrame ?? ((handle: number) => cancelAnimationFrame(handle));
  const now = options.now ?? (() => performance.now());
  const onDetectError = options.onDetectError ?? createWarnOnce();
  const log = options.log ?? ((summary: string) => console.info(summary));

  const listeners = new Set<(sample: GazeSample) => void>();
  let running = false;
  let starting: Promise<void> | null = null;
  let generation = 0;
  let stream: CameraStreamLike | null = null;
  let landmarker: LandmarkerLike | null = null;
  let video: VideoLike | null = null;
  let frameHandle: number | null = null;
  let lastVideoTime = -1;
  let lastLogAt = 0;
  let counters = emptyCounters();

  const emit = (sample: GazeSample) => listeners.forEach((listener) => listener(sample));

  function releaseResources() {
    if (frameHandle !== null) cancelFrame(frameHandle);
    frameHandle = null;
    stream?.getTracks().forEach((track) => track.stop());
    landmarker?.close();
    video?.pause();
    if (video) video.srcObject = null;
    video?.remove?.();
    stream = null;
    landmarker = null;
    video = null;
  }

  function detect(currentVideo: VideoLike, currentLandmarker: LandmarkerLike, t: number): BlendshapeScore[] | null {
    try {
      return currentLandmarker.detectForVideo(currentVideo, t).faceBlendshapes?.[0]?.categories ?? null;
    } catch (failure) {
      // A failed detection is one frame without a face. The next frame may work again.
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
    };
  }

  function summary() {
    const d = diagnostics();
    return `[eye] frames=${d.frames} withFace=${d.withFace} incomplete=${d.incomplete} skipped=${d.skipped} missing=${d.missing.join(",") || "-"} video=${d.videoWidth}x${d.videoHeight} ready=${d.readyState}`;
  }

  function count(categories: BlendshapeScore[] | null) {
    counters.frames++;
    if (!categories) return;
    counters.withFace++;
    const missing = missingEyeBlendshapes(categories);
    if (missing.length === 0) return;
    counters.incomplete++;
    counters.missing = missing;
  }

  function processFrame(currentVideo: VideoLike, currentLandmarker: LandmarkerLike) {
    if (currentVideo.readyState < HAVE_CURRENT_DATA || currentVideo.currentTime === lastVideoTime) {
      counters.skipped++;
      return;
    }
    lastVideoTime = currentVideo.currentTime;
    const t = now();
    const categories = detect(currentVideo, currentLandmarker, t);
    count(categories);
    emit(processor.process({ t, blendshapes: categories }));
  }

  function tick() {
    if (!running || !video || !landmarker) return;
    frameHandle = requestFrame(tick);
    processFrame(video, landmarker);
    if (now() - lastLogAt < LOG_EVERY_MS) return;
    lastLogAt = now();
    log(summary());
  }

  async function open(myGeneration: number) {
    if (!isCameraSupported(navigatorLike)) {
      throw new CameraError("unsupported", "This browser cannot use the camera here (it needs https or localhost).");
    }
    const cancelled = () => myGeneration !== generation;

    let openedStream: CameraStreamLike;
    try {
      openedStream = await getStream();
    } catch (failure) {
      throw new CameraError(classifyCameraError(failure), "The camera could not be opened.", failure);
    }
    stream = openedStream;
    if (cancelled()) return releaseResources();

    try {
      landmarker = await loadLandmarker();
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
    lastLogAt = now();
    frameHandle = requestFrame(tick);
  }

  return {
    async start() {
      if (running) return;
      starting ??= open(generation).finally(() => {
        starting = null;
      });
      await starting;
    },

    stop() {
      generation++;
      running = false;
      releaseResources();
    },

    subscribe(listener) {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },

    setCalibration(calibration) {
      processor.setCalibration(calibration);
    },

    getDiagnostics: diagnostics,
  };
}
