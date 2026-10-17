import type { BlendshapeScore } from "./blendshapes";
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
  srcObject: unknown;
  muted: boolean;
  playsInline: boolean;
  play(): Promise<unknown>;
  pause(): void;
}

export interface CameraStreamLike {
  getTracks(): Array<{ stop(): void }>;
}

export interface CameraGazeSource extends GazeSource {
  setCalibration(calibration: Calibration): void;
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
}

/** Both a video frame has to be decoded (HAVE_CURRENT_DATA) before the model can look at it. */
const HAVE_CURRENT_DATA = 2;

const defaultGetStream = () =>
  navigator.mediaDevices.getUserMedia({
    video: { width: { ideal: 640 }, height: { ideal: 480 }, facingMode: "user" },
    audio: false,
  });

function defaultCreateVideo(): VideoLike {
  return document.createElement("video");
}

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

  const listeners = new Set<(sample: GazeSample) => void>();
  let running = false;
  let starting: Promise<void> | null = null;
  let generation = 0;
  let stream: CameraStreamLike | null = null;
  let landmarker: LandmarkerLike | null = null;
  let video: VideoLike | null = null;
  let frameHandle: number | null = null;
  let lastVideoTime = -1;

  const emit = (sample: GazeSample) => listeners.forEach((listener) => listener(sample));

  function releaseResources() {
    if (frameHandle !== null) cancelFrame(frameHandle);
    frameHandle = null;
    stream?.getTracks().forEach((track) => track.stop());
    landmarker?.close();
    video?.pause();
    if (video) video.srcObject = null;
    stream = null;
    landmarker = null;
    video = null;
  }

  function detect(currentVideo: VideoLike, currentLandmarker: LandmarkerLike, t: number): BlendshapeScore[] | null {
    try {
      return currentLandmarker.detectForVideo(currentVideo, t).faceBlendshapes?.[0]?.categories ?? null;
    } catch {
      // A failed detection is one frame without a face. The next frame may work again.
      return null;
    }
  }

  function tick() {
    if (!running || !video || !landmarker) return;
    frameHandle = requestFrame(tick);
    if (video.readyState < HAVE_CURRENT_DATA || video.currentTime === lastVideoTime) return;

    lastVideoTime = video.currentTime;
    const t = now();
    emit(processor.process({ t, blendshapes: detect(video, landmarker, t) }));
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
  };
}
