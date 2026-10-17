import { describe, expect, it } from "vitest";
import {
  DEFAULT_FACE_LANDMARKER_MODEL_URL,
  DEFAULT_MANAGER_URL,
  DEFAULT_MEDIAPIPE_WASM_URL,
  MEDIAPIPE_VERSION,
  resolveFaceLandmarkerUrls,
  resolveManagerUrl,
} from "./env";

describe("resolveManagerUrl", () => {
  it("uses VITE_MANAGER_URL when set", () => {
    expect(resolveManagerUrl({ VITE_MANAGER_URL: "https://manager.example.com" })).toBe(
      "https://manager.example.com"
    );
  });

  it("drops trailing slashes", () => {
    expect(resolveManagerUrl({ VITE_MANAGER_URL: "https://manager.example.com//" })).toBe(
      "https://manager.example.com"
    );
  });

  it("trims whitespace", () => {
    expect(resolveManagerUrl({ VITE_MANAGER_URL: "  http://localhost:1  " })).toBe("http://localhost:1");
  });

  it.each([undefined, "", "   "])("falls back to the default for %j", (value) => {
    expect(resolveManagerUrl({ VITE_MANAGER_URL: value })).toBe(DEFAULT_MANAGER_URL);
  });
});

describe("resolveFaceLandmarkerUrls", () => {
  it("defaults to the official model and the matching MediaPipe wasm build", () => {
    const urls = resolveFaceLandmarkerUrls({});

    expect(urls.modelUrl).toBe(DEFAULT_FACE_LANDMARKER_MODEL_URL);
    expect(urls.wasmUrl).toBe(DEFAULT_MEDIAPIPE_WASM_URL);
    expect(urls.wasmUrl).toContain(`@mediapipe/tasks-vision@${MEDIAPIPE_VERSION}/wasm`);
  });

  it("takes both urls from the environment", () => {
    const urls = resolveFaceLandmarkerUrls({
      VITE_FACE_LANDMARKER_MODEL_URL: "/models/face_landmarker.task",
      VITE_MEDIAPIPE_WASM_URL: "/mediapipe/wasm/",
    });

    expect(urls).toEqual({ modelUrl: "/models/face_landmarker.task", wasmUrl: "/mediapipe/wasm" });
  });

  it("falls back per url when one is blank", () => {
    const urls = resolveFaceLandmarkerUrls({ VITE_FACE_LANDMARKER_MODEL_URL: "  ", VITE_MEDIAPIPE_WASM_URL: "/wasm" });

    expect(urls.modelUrl).toBe(DEFAULT_FACE_LANDMARKER_MODEL_URL);
    expect(urls.wasmUrl).toBe("/wasm");
  });
});
