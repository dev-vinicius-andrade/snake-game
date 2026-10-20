import { resolveFaceLandmarkerUrls } from "@/config/env";
import { createWorkerLandmarker } from "./workerLandmarker";
import type { LandmarkerLike } from "./mediapipeGazeSource";

type Urls = ReturnType<typeof resolveFaceLandmarkerUrls>;

export interface LoaderParts {
  /** Whether this browser can run the model on a worker thread. */
  canUseWorker(): boolean;
  startWorker(urls: Urls): Promise<LandmarkerLike>;
  startMainThread(urls: Urls): Promise<LandmarkerLike>;
}

/** Workers and bitmaps are needed to run the model off the page; a flag in storage turns it off for finding out what a worker changes. */
const canUseWorkerHere = () => {
  try {
    if (typeof localStorage !== "undefined" && localStorage.getItem("snake.faceWorker") === "off") return false;
  } catch {
    // No storage: the default applies.
  }
  return typeof Worker !== "undefined" && typeof createImageBitmap === "function";
};

async function startWorker(urls: Urls): Promise<LandmarkerLike> {
  const landmarker = await createWorkerLandmarker(urls, () => new Worker(new URL("./faceWorker.ts", import.meta.url), { type: "module" }) as never);
  // A worker answers later; the camera source copes with answers that are promises.
  return landmarker as unknown as LandmarkerLike;
}

/** The model on the page itself: the way it was before the worker, and the way back when a worker cannot start. */
async function startMainThread(urls: Urls): Promise<LandmarkerLike> {
  const { FaceLandmarker, FilesetResolver } = await import("@mediapipe/tasks-vision");
  const fileset = await FilesetResolver.forVisionTasks(urls.wasmUrl);
  const create = (delegate: "GPU" | "CPU") =>
    FaceLandmarker.createFromOptions(fileset, {
      baseOptions: { modelAssetPath: urls.modelUrl, delegate },
      runningMode: "VIDEO",
      numFaces: 1,
      outputFaceBlendshapes: true,
      // Only the head mode reads the transformation matrix: the source turns it on when it is needed.
      outputFacialTransformationMatrixes: false,
    });

  const landmarker = await create("GPU").catch(() => create("CPU"));
  // The video handed in by the gaze source is an HTMLVideoElement; `VideoLike` only names what the source uses of it.
  return {
    detectForVideo: (video, timestampMs) => landmarker.detectForVideo(video as unknown as HTMLVideoElement, timestampMs),
    setOutputs: ({ matrices }) => landmarker.setOptions({ outputFacialTransformationMatrixes: matrices }),
    close: () => landmarker.close(),
  };
}

/**
 * Loads MediaPipe Face Landmarker with blendshapes. The library is imported on demand, so players who never turn on camera gaze do not
 * download it. It runs on a worker thread when the browser allows, so finding the face does not hold up drawing the game, and on the
 * page otherwise. Tries the GPU first and falls back to the CPU.
 */
export async function loadFaceLandmarker(
  urls: Urls = resolveFaceLandmarkerUrls(),
  parts: LoaderParts = { canUseWorker: canUseWorkerHere, startWorker, startMainThread }
): Promise<LandmarkerLike> {
  if (parts.canUseWorker()) {
    try {
      return await parts.startWorker(urls);
    } catch (failure) {
      console.warn("[eye] the face worker could not start, running the model on the page:", failure);
    }
  }
  return parts.startMainThread(urls);
}
