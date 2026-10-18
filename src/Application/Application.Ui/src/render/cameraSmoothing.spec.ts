import { describe, expect, it } from "vitest";
import { CameraSmoother } from "./cameraSmoothing";

const at = (x: number, y = 0) => ({ x, y });

describe("CameraSmoother", () => {
  it("starts exactly on the first target, so the camera does not fly in from nowhere", () => {
    const smoother = new CameraSmoother({ snapTo: 0 });

    expect(smoother.step(at(400, 200), 0)).toEqual(at(400, 200));
  });

  it("moves toward a new target without reaching it at once", () => {
    const smoother = new CameraSmoother({ snapTo: 0 });
    smoother.step(at(0), 0);

    const next = smoother.step(at(100), 16);

    expect(next.x).toBeGreaterThan(0);
    expect(next.x).toBeLessThan(100);
  });

  it("keeps closing in on a target that stays put", () => {
    const smoother = new CameraSmoother({ snapTo: 0 });
    smoother.step(at(0), 0);

    let last = 0;
    for (let t = 16; t <= 800; t += 16) {
      const x = smoother.step(at(100), t).x;
      expect(x).toBeGreaterThanOrEqual(last);
      last = x;
    }

    expect(last).toBeGreaterThan(99);
  });

  it("never overshoots the target", () => {
    const smoother = new CameraSmoother({ snapTo: 0 });
    smoother.step(at(0), 0);

    for (let t = 16; t <= 2000; t += 16) expect(smoother.step(at(100), t).x).toBeLessThanOrEqual(100);
  });

  it("covers the same ground whatever the frame rate", () => {
    const fast = new CameraSmoother({ snapTo: 0 });
    const slow = new CameraSmoother({ snapTo: 0 });
    fast.step(at(0), 0);
    slow.step(at(0), 0);

    for (let t = 8; t <= 160; t += 8) fast.step(at(100), t);
    const slowEnd = slow.step(at(100), 160);

    expect(fast.step(at(100), 160).x).toBeCloseTo(slowEnd.x, 0);
  });

  it("covers the same ground however often it is asked within a frame", () => {
    const once = new CameraSmoother({ snapTo: 0 });
    const often = new CameraSmoother({ snapTo: 0 });
    once.step(at(0), 0);
    often.step(at(0), 0);

    once.step(at(100), 16);
    [4, 8, 12, 16].forEach((t) => often.step(at(100), t));

    expect(often.step(at(100), 16).x).toBeCloseTo(once.step(at(100), 16).x, 1);
  });

  it("handles each axis on its own", () => {
    const smoother = new CameraSmoother({ snapTo: 0 });
    smoother.step(at(0, 0), 0);

    const next = smoother.step(at(100, 0), 16);

    expect(next.y).toBe(0);
  });

  it("jumps straight to a target that is very far, for example after a respawn", () => {
    const smoother = new CameraSmoother({ snapTo: 0, jumpDistance: 500 });
    smoother.step(at(0), 0);

    expect(smoother.step(at(5000), 16)).toEqual(at(5000));
  });

  it("jumps after a long pause, such as a hidden tab", () => {
    const smoother = new CameraSmoother({ snapTo: 0 });
    smoother.step(at(0), 0);

    expect(smoother.step(at(100), 10_000)).toEqual(at(100));
  });

  it("starts over after a reset", () => {
    const smoother = new CameraSmoother({ snapTo: 0 });
    smoother.step(at(0), 0);

    smoother.reset();

    expect(smoother.step(at(300), 16)).toEqual(at(300));
  });

  it("snaps the result to whole logical pixels, so the pixel art does not shimmer", () => {
    const smoother = new CameraSmoother({ snapTo: 5 });
    smoother.step(at(0), 0);

    const next = smoother.step(at(100), 16);

    expect(next.x % 5).toBe(0);
  });

  it("still reaches the target when snapping", () => {
    const smoother = new CameraSmoother({ snapTo: 5 });
    smoother.step(at(0), 0);

    let last = at(0);
    for (let t = 16; t <= 2000; t += 16) last = smoother.step(at(100), t);

    expect(last.x).toBe(100);
  });

  it("is slower to start with a larger time constant", () => {
    const quick = new CameraSmoother({ snapTo: 0, timeConstantMs: 50 });
    const slowly = new CameraSmoother({ snapTo: 0, timeConstantMs: 400 });
    quick.step(at(0), 0);
    slowly.step(at(0), 0);

    expect(quick.step(at(100), 16).x).toBeGreaterThan(slowly.step(at(100), 16).x);
  });
});
