import type { BlendshapeScore } from "./blendshapes";
import type { HeadMatrix } from "./headPose";
import type { LandmarkerResultLike } from "./mediapipeGazeSource";

/** What the worker sends back for a frame: only what the app reads, small enough to send 30 times a second. */
export interface LeanResult {
  /** A face was found. */
  face: boolean;
  categories: BlendshapeScore[] | null;
  matrix: HeadMatrix | null;
}

/** The messages the page sends to the face worker. */
export type WorkerRequest =
  | { type: "init"; wasmUrl: string; modelUrl: string }
  | { type: "detect"; id: number; bitmap: ImageBitmap; t: number }
  | { type: "outputs"; matrices: boolean }
  | { type: "close" };

/** The messages the face worker sends back. */
export type WorkerReply =
  | { type: "ready"; delegate: "GPU" | "CPU" }
  | { type: "failed"; message: string }
  | { type: "result"; id: number; lean: LeanResult }
  | { type: "error"; id: number; message: string };

interface FullResult {
  faceLandmarks?: unknown[];
  faceBlendshapes?: Array<{ categories: Array<{ categoryName: string; score: number }> }>;
  facialTransformationMatrixes?: Array<{ data: ArrayLike<number> }>;
}

/** MediaPipe's result cut down to what is needed, with the numbers as plain arrays. Runs in the worker. */
export function toLean(result: FullResult): LeanResult {
  const face = (result.faceLandmarks?.length ?? 0) > 0;
  const shapes = result.faceBlendshapes?.[0]?.categories;
  const matrix = result.facialTransformationMatrixes?.[0];
  return {
    face,
    categories: shapes ? shapes.map(({ categoryName, score }) => ({ categoryName, score })) : null,
    matrix: matrix ? { data: Array.from(matrix.data) } : null,
  };
}

/** The result the camera source reads, rebuilt from a lean one. Runs on the page. */
export function fromLean(lean: LeanResult): LandmarkerResultLike {
  return {
    faceLandmarks: lean.face ? [[]] : [],
    faceBlendshapes: lean.categories ? [{ categories: lean.categories }] : [],
    facialTransformationMatrixes: lean.matrix ? [lean.matrix] : [],
  };
}
