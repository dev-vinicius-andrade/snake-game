/// <reference lib="webworker" />
import type { FaceLandmarker } from "@mediapipe/tasks-vision";
import { toLean, type WorkerReply, type WorkerRequest } from "./faceWorkerProtocol";

/**
 * The face model on its own thread. The page copies each video frame to a bitmap and sends it here; the answer is cut down to what the
 * app reads (see faceWorkerProtocol). The frames and the answers never leave the browser.
 */
const scope = self as unknown as DedicatedWorkerGlobalScope;
let landmarker: FaceLandmarker | null = null;

const reply = (message: WorkerReply) => scope.postMessage(message);

async function start(wasmUrl: string, modelUrl: string) {
  const { FaceLandmarker, FilesetResolver } = await import("@mediapipe/tasks-vision");
  // Module workers cannot import scripts the old way, so the wasm loader that is an ES module is used.
  const fileset = await FilesetResolver.forVisionTasks(wasmUrl, true);
  const create = (delegate: "GPU" | "CPU") =>
    FaceLandmarker.createFromOptions(fileset, {
      baseOptions: { modelAssetPath: modelUrl, delegate },
      runningMode: "VIDEO",
      numFaces: 1,
      outputFaceBlendshapes: true,
      outputFacialTransformationMatrixes: false,
    });
  try {
    landmarker = await create("GPU");
    reply({ type: "ready", delegate: "GPU" });
  } catch {
    landmarker = await create("CPU");
    reply({ type: "ready", delegate: "CPU" });
  }
}

scope.onmessage = async ({ data }: MessageEvent<WorkerRequest>) => {
  if (data.type === "init") {
    try {
      await start(data.wasmUrl, data.modelUrl);
    } catch (failure) {
      reply({ type: "failed", message: failure instanceof Error ? failure.message : String(failure) });
    }
  } else if (data.type === "detect") {
    try {
      if (!landmarker) throw new Error("The face model is not loaded.");
      reply({ type: "result", id: data.id, lean: toLean(landmarker.detectForVideo(data.bitmap, data.t)) });
    } catch (failure) {
      reply({ type: "error", id: data.id, message: failure instanceof Error ? failure.message : String(failure) });
    } finally {
      data.bitmap.close();
    }
  } else if (data.type === "outputs") {
    landmarker?.setOptions({ outputFacialTransformationMatrixes: data.matrices });
  } else if (data.type === "close") {
    landmarker?.close();
    landmarker = null;
  }
};
