import { describe, expect, it, vi } from "vitest";
import { loadFaceLandmarker } from "./faceLandmarker";
import type { LandmarkerLike } from "./mediapipeGazeSource";

const urls = { wasmUrl: "http://wasm", modelUrl: "http://model" };
const fake = (name: string): LandmarkerLike => ({ detectForVideo: () => ({ faceLandmarks: [[{ name }]] }), close: () => undefined });

describe("loadFaceLandmarker", () => {
  it("runs the model on a worker when the browser can", async () => {
    const worker = fake("worker");
    const mainThread = vi.fn(async () => fake("main"));

    const landmarker = await loadFaceLandmarker(urls, { canUseWorker: () => true, startWorker: async () => worker, startMainThread: mainThread });

    expect(landmarker).toBe(worker);
    expect(mainThread).not.toHaveBeenCalled();
  });

  it("runs it on the page when the worker cannot start", async () => {
    const main = fake("main");

    const landmarker = await loadFaceLandmarker(urls, {
      canUseWorker: () => true,
      startWorker: async () => {
        throw new Error("no module workers");
      },
      startMainThread: async () => main,
    });

    expect(landmarker).toBe(main);
  });

  it("runs it on the page when the browser has no workers or no bitmaps", async () => {
    const main = fake("main");
    const startWorker = vi.fn(async () => fake("worker"));

    const landmarker = await loadFaceLandmarker(urls, { canUseWorker: () => false, startWorker, startMainThread: async () => main });

    expect(landmarker).toBe(main);
    expect(startWorker).not.toHaveBeenCalled();
  });

  it("fails when neither can start", async () => {
    await expect(
      loadFaceLandmarker(urls, {
        canUseWorker: () => true,
        startWorker: async () => {
          throw new Error("a");
        },
        startMainThread: async () => {
          throw new Error("b");
        },
      })
    ).rejects.toThrow("b");
  });
});
