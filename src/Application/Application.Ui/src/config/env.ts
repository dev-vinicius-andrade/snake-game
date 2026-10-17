export const DEFAULT_MANAGER_URL = "http://localhost:60510";

export interface ManagerEnv {
  VITE_MANAGER_URL?: string;
}

/** Base url of the manager api: `VITE_MANAGER_URL` without trailing slashes, or the local docker default. */
export function resolveManagerUrl(env: ManagerEnv = import.meta.env): string {
  const configured = env.VITE_MANAGER_URL?.trim();
  return configured ? configured.replace(/\/+$/, "") : DEFAULT_MANAGER_URL;
}

/** Version of `@mediapipe/tasks-vision` in package.json. The wasm files must come from the same version. */
export const MEDIAPIPE_VERSION = "1.1.1";
export const DEFAULT_MEDIAPIPE_WASM_URL = `https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@${MEDIAPIPE_VERSION}/wasm`;
export const DEFAULT_FACE_LANDMARKER_MODEL_URL =
  "https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task";

export interface FaceLandmarkerEnv {
  VITE_FACE_LANDMARKER_MODEL_URL?: string;
  VITE_MEDIAPIPE_WASM_URL?: string;
}

/**
 * Where the camera gaze loads the face model and the MediaPipe wasm from. Defaults are the official hosts; set both
 * variables to self hosted copies to avoid third party requests. Camera frames never leave the browser either way.
 */
export function resolveFaceLandmarkerUrls(env: FaceLandmarkerEnv = import.meta.env) {
  const model = env.VITE_FACE_LANDMARKER_MODEL_URL?.trim();
  const wasm = env.VITE_MEDIAPIPE_WASM_URL?.trim();
  return {
    modelUrl: model || DEFAULT_FACE_LANDMARKER_MODEL_URL,
    wasmUrl: wasm ? wasm.replace(/\/+$/, "") : DEFAULT_MEDIAPIPE_WASM_URL,
  };
}
