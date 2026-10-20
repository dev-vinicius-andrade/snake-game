import { fromLean, type WorkerReply, type WorkerRequest } from "./faceWorkerProtocol";
import type { LandmarkerLike, LandmarkerResultLike, VideoLike } from "./mediapipeGazeSource";

/** The part of a `Worker` this uses. */
export interface WorkerLike {
  postMessage(message: WorkerRequest, transfer?: Transferable[]): void;
  terminate(): void;
  onmessage: ((event: { data: WorkerReply }) => void) | null;
  onerror: ((event: { message?: string }) => void) | null;
}

/** A face landmarker that answers later: the work is done on another thread. Ask for one frame at a time. */
export interface AsyncLandmarker extends Omit<LandmarkerLike, "detectForVideo"> {
  detectForVideo(video: VideoLike, timestampMs: number): Promise<LandmarkerResultLike>;
}

export interface FrameCapture {
  /** A copy of the current video frame the worker can own. */
  createBitmap(video: VideoLike): Promise<ImageBitmap>;
}

const START_TIMEOUT_MS = 30_000;

const defaultCapture: FrameCapture = { createBitmap: (video) => createImageBitmap(video as unknown as HTMLVideoElement) };

/**
 * Runs the face model on a worker thread, so finding the face never holds up drawing the game. Resolves when the model is loaded there
 * and rejects when it cannot start (the caller then runs it on the page). Each frame is copied to a bitmap and handed over.
 */
export function createWorkerLandmarker(
  urls: { wasmUrl: string; modelUrl: string },
  createWorker: () => WorkerLike,
  capture: FrameCapture = defaultCapture,
  startTimeoutMs: number = START_TIMEOUT_MS
): Promise<AsyncLandmarker> {
  const worker = createWorker();
  const waiting = new Map<number, { resolve: (result: LandmarkerResultLike) => void; reject: (failure: Error) => void }>();
  let nextId = 1;
  let closed = false;

  const stop = () => {
    closed = true;
    worker.terminate();
    waiting.forEach(({ reject }) => reject(new Error("The face worker was closed.")));
    waiting.clear();
  };

  const landmarker: AsyncLandmarker = {
    async detectForVideo(video, timestampMs) {
      if (closed) throw new Error("The face worker was closed.");
      const bitmap = await capture.createBitmap(video);
      if (closed) {
        bitmap.close?.();
        throw new Error("The face worker was closed.");
      }
      const id = nextId++;
      return new Promise<LandmarkerResultLike>((resolve, reject) => {
        waiting.set(id, { resolve, reject });
        worker.postMessage({ type: "detect", id, bitmap, t: timestampMs }, [bitmap]);
      });
    },
    setOutputs(outputs) {
      if (!closed) worker.postMessage({ type: "outputs", matrices: outputs.matrices });
    },
    close() {
      if (closed) return;
      worker.postMessage({ type: "close" });
      stop();
    },
  };

  return new Promise<AsyncLandmarker>((resolve, reject) => {
    const failToStart = (message: string) => {
      stop();
      reject(new Error(message));
    };
    const timer = setTimeout(() => failToStart("The face worker did not start in time."), startTimeoutMs);

    worker.onerror = (event) => {
      clearTimeout(timer);
      failToStart(event.message || "The face worker failed.");
    };
    worker.onmessage = ({ data }) => {
      if (data.type === "ready") {
        clearTimeout(timer);
        resolve(landmarker);
      } else if (data.type === "failed") {
        clearTimeout(timer);
        failToStart(data.message);
      } else if (data.type === "result") {
        waiting.get(data.id)?.resolve(fromLean(data.lean));
        waiting.delete(data.id);
      } else if (data.type === "error") {
        waiting.get(data.id)?.reject(new Error(data.message));
        waiting.delete(data.id);
      }
    };
    worker.postMessage({ type: "init", wasmUrl: urls.wasmUrl, modelUrl: urls.modelUrl });
  });
}
