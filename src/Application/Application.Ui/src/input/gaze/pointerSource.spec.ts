import { describe, expect, it, vi } from "vitest";
import type { GazeSample } from "./gazeSource";
import { createPointerSource, type PointerEventTarget } from "./pointerSource";

class FakeTarget implements PointerEventTarget {
  private readonly listeners = new Map<string, Set<(event: any) => void>>();
  addEventListener(type: string, listener: (event: any) => void) {
    this.listeners.set(type, (this.listeners.get(type) ?? new Set()).add(listener));
  }
  removeEventListener(type: string, listener: (event: any) => void) {
    this.listeners.get(type)?.delete(listener);
  }
  count(type: string) {
    return this.listeners.get(type)?.size ?? 0;
  }
  move(clientX: number, clientY: number, timeStamp = 0) {
    this.listeners.get("pointermove")?.forEach((listener) => listener({ clientX, clientY, timeStamp }));
  }
}

const rect = { left: 100, top: 50, width: 800, height: 450 };

function setup(getRect: () => typeof rect | null = () => rect) {
  const target = new FakeTarget();
  const source = createPointerSource({ target, getRect });
  const samples: GazeSample[] = [];
  source.subscribe((sample) => samples.push(sample));
  return { target, source, samples };
}

describe("createPointerSource", () => {
  it("is silent until started", () => {
    const { target, samples } = setup();

    target.move(500, 275);

    expect(samples).toEqual([]);
  });

  it("turns pointer moves into stage coordinates", async () => {
    const { target, source, samples } = setup();
    await source.start();

    target.move(500, 275, 1234);

    expect(samples).toEqual([{ t: 1234, x: 0.5, y: 0.5 }]);
  });

  it("keeps positions outside the stage", async () => {
    const { target, source, samples } = setup();
    await source.start();

    target.move(1700, 50, 10);

    expect(samples[0].x).toBe(2);
  });

  it("skips moves while the stage has no size", async () => {
    const { target, source, samples } = setup(() => null);
    await source.start();

    target.move(500, 275);

    expect(samples).toEqual([]);
  });

  it("stops listening after stop", async () => {
    const { target, source, samples } = setup();
    await source.start();

    source.stop();
    target.move(500, 275);

    expect(samples).toEqual([]);
    expect(target.count("pointermove")).toBe(0);
  });

  it("does not double up when started twice", async () => {
    const { target, source, samples } = setup();
    await source.start();
    await source.start();

    target.move(500, 275);

    expect(samples).toHaveLength(1);
    expect(target.count("pointermove")).toBe(1);
  });

  it("can be started again after stop", async () => {
    const { target, source, samples } = setup();
    await source.start();
    source.stop();
    await source.start();

    target.move(500, 275);

    expect(samples).toHaveLength(1);
  });

  it("stops delivering to a listener that unsubscribed", async () => {
    const { target, source } = setup();
    const listener = vi.fn();
    const unsubscribe = source.subscribe(listener);
    await source.start();

    unsubscribe();
    target.move(500, 275);

    expect(listener).not.toHaveBeenCalled();
  });

  it("delivers to every subscriber", async () => {
    const { target, source } = setup();
    const second = vi.fn();
    source.subscribe(second);
    await source.start();

    target.move(500, 275);

    expect(second).toHaveBeenCalledTimes(1);
  });
});
