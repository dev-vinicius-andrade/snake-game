import { describe, expect, it, vi } from "vitest";
import type { WorkerReply, WorkerRequest } from "./faceWorkerProtocol";
import { createWorkerLandmarker, type WorkerLike } from "./workerLandmarker";

class FakeWorker implements WorkerLike {
  sent: WorkerRequest[] = [];
  terminated = false;
  onmessage: ((event: { data: WorkerReply }) => void) | null = null;
  onerror: ((event: { message?: string }) => void) | null = null;
  /** What it answers to an init: left empty, the test answers itself. */
  autoReady: WorkerReply | null = { type: "ready", delegate: "GPU" };

  postMessage(message: WorkerRequest) {
    this.sent.push(message);
    if (message.type === "init" && this.autoReady) queueMicrotask(() => this.reply(this.autoReady!));
  }

  terminate() {
    this.terminated = true;
  }

  reply(data: WorkerReply) {
    this.onmessage?.({ data });
  }
}

const urls = { wasmUrl: "http://wasm", modelUrl: "http://model" };
const bitmap = { close: vi.fn() } as unknown as ImageBitmap;
const frame = (): Parameters<typeof createWorkerLandmarker>[2] => ({ createBitmap: async () => bitmap });

async function ready(worker = new FakeWorker()) {
  const landmarker = await createWorkerLandmarker(urls, () => worker, frame());
  return { worker, landmarker };
}

describe("createWorkerLandmarker", () => {
  it("starts the worker with the model and waits until it is ready", async () => {
    const { worker } = await ready();

    expect(worker.sent[0]).toEqual({ type: "init", wasmUrl: "http://wasm", modelUrl: "http://model" });
  });

  it("fails when the worker cannot load the model, and ends it", async () => {
    const worker = new FakeWorker();
    worker.autoReady = { type: "failed", message: "no gpu" };

    await expect(createWorkerLandmarker(urls, () => worker, frame())).rejects.toThrow("no gpu");

    expect(worker.terminated).toBe(true);
  });

  it("fails when the worker itself breaks", async () => {
    const worker = new FakeWorker();
    worker.autoReady = null;
    const pending = createWorkerLandmarker(urls, () => worker, frame());

    worker.onerror?.({ message: "boom" });

    await expect(pending).rejects.toThrow("boom");
    expect(worker.terminated).toBe(true);
  });

  it("gives up when the worker never answers", async () => {
    vi.useFakeTimers();
    const worker = new FakeWorker();
    worker.autoReady = null;
    const pending = createWorkerLandmarker(urls, () => worker, frame(), 5000);
    const outcome = expect(pending).rejects.toThrow(/did not start/);

    await vi.advanceTimersByTimeAsync(5001);

    await outcome;
    expect(worker.terminated).toBe(true);
    vi.useRealTimers();
  });

  it("sends a frame as a bitmap and answers with the result the camera source reads", async () => {
    const { worker, landmarker } = await ready();

    const pending = landmarker.detectForVideo({} as never, 1234);
    await Promise.resolve();
    await Promise.resolve();
    const request = worker.sent.find((message) => message.type === "detect");
    expect(request).toMatchObject({ type: "detect", t: 1234 });
    worker.reply({ type: "result", id: (request as { id: number }).id, lean: { face: true, categories: [{ categoryName: "jawOpen", score: 0.5 }], matrix: null } });

    const result = await pending;
    expect(result.faceLandmarks).toHaveLength(1);
    expect(result.faceBlendshapes?.[0].categories[0].score).toBe(0.5);
  });

  it("rejects a frame the worker could not process", async () => {
    const { worker, landmarker } = await ready();

    const pending = landmarker.detectForVideo({} as never, 1);
    const outcome = expect(pending).rejects.toThrow("bad frame");
    await Promise.resolve();
    await Promise.resolve();
    const request = worker.sent.find((message) => message.type === "detect") as { id: number };
    worker.reply({ type: "error", id: request.id, message: "bad frame" });

    await outcome;
  });

  it("tells the worker which outputs are wanted", async () => {
    const { worker, landmarker } = await ready();

    landmarker.setOutputs?.({ matrices: true });

    expect(worker.sent.at(-1)).toEqual({ type: "outputs", matrices: true });
  });

  it("ends the worker on close, and answers frames still waiting with a failure", async () => {
    const { worker, landmarker } = await ready();
    const pending = landmarker.detectForVideo({} as never, 1);
    const outcome = expect(pending).rejects.toThrow(/closed/);
    await Promise.resolve();
    await Promise.resolve();

    landmarker.close();

    await outcome;
    expect(worker.terminated).toBe(true);
    expect(worker.sent.some((message) => message.type === "close")).toBe(true);
  });
});
