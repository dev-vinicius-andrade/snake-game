import { describe, expect, it } from "vitest";
import { CameraSmoother } from "./cameraSmoothing";

const world = { width: 1920, height: 1080 };
const at = (x: number, y = 0) => ({ x, y });

describe("CameraSmoother", () => {
  it("starts exactly on the first target, so the camera does not fly in from nowhere", () => {
    expect(new CameraSmoother().step(at(400, 200), 0, world)).toEqual(at(400, 200));
  });

  it("moves toward a new target without reaching it at once", () => {
    const smoother = new CameraSmoother();
    smoother.step(at(100), 0, world);

    const next = smoother.step(at(160), 16, world);

    expect(next.x).toBeGreaterThan(100);
    expect(next.x).toBeLessThan(160);
  });

  it("keeps closing in on a target that stays put, and gets there", () => {
    const smoother = new CameraSmoother();
    smoother.step(at(100), 0, world);

    let last = 100;
    for (let t = 16; t <= 1500; t += 16) {
      const x = smoother.step(at(200), t, world).x;
      expect(x).toBeGreaterThanOrEqual(last);
      last = x;
    }

    expect(last).toBeGreaterThan(199.9);
  });

  it("never overshoots the target", () => {
    const smoother = new CameraSmoother();
    smoother.step(at(100), 0, world);

    for (let t = 16; t <= 3000; t += 16) expect(smoother.step(at(200), t, world).x).toBeLessThanOrEqual(200);
  });

  it("covers the same ground whatever the frame rate", () => {
    const fast = new CameraSmoother();
    const slow = new CameraSmoother();
    fast.step(at(100), 0, world);
    slow.step(at(100), 0, world);

    for (let t = 8; t <= 160; t += 8) fast.step(at(200), t, world);

    expect(fast.step(at(200), 160, world).x).toBeCloseTo(slow.step(at(200), 160, world).x, 0);
  });

  it("covers the same ground however often it is asked inside one frame", () => {
    const once = new CameraSmoother();
    const often = new CameraSmoother();
    once.step(at(100), 0, world);
    often.step(at(100), 0, world);

    once.step(at(200), 16, world);
    [4, 8, 12, 16].forEach((t) => often.step(at(200), t, world));

    expect(often.step(at(200), 16, world).x).toBeCloseTo(once.step(at(200), 16, world).x, 1);
  });

  it("handles each axis on its own", () => {
    const smoother = new CameraSmoother();
    smoother.step(at(100, 100), 0, world);

    const next = smoother.step(at(160, 100), 16, world);

    expect(next.y).toBe(100);
  });

  it("smooths out a target that flips back and forth, which is what a camera shake is", () => {
    const smoother = new CameraSmoother();
    smoother.step(at(500), 0, world);

    // The target moves 6 units a frame, then 6 units back, then forth again: turning left and right in quick succession.
    const xs: number[] = [];
    for (let frame = 1; frame <= 40; frame++) xs.push(smoother.step(at(500 + (frame % 2 === 0 ? 0 : 6)), frame * 8, world).x);

    const swing = Math.max(...xs.slice(10)) - Math.min(...xs.slice(10));
    expect(swing).toBeLessThan(3);
  });

  describe("on a world that wraps around", () => {
    it("follows a target that crosses an edge without gliding back across the world", () => {
      const smoother = new CameraSmoother();
      smoother.step(at(1915), 0, world);

      const next = smoother.step(at(5), 16, world);

      // The short way is 10 units forward, through the edge: about 1915 + a few, not a trip back across 1900 units.
      expect(next.x).toBeGreaterThan(1915);
      expect(next.x).toBeLessThan(1930);
    });

    it("ends up on the target, in the world's terms, after crossing", () => {
      const smoother = new CameraSmoother();
      smoother.step(at(1915), 0, world);

      let last = at(0);
      for (let t = 16; t <= 1500; t += 16) last = smoother.step(at(5), t, world);

      const wrapped = ((last.x % 1920) + 1920) % 1920;
      expect(wrapped).toBeCloseTo(5, 1);
    });

    it("also works across the top and bottom edge", () => {
      const smoother = new CameraSmoother();
      smoother.step(at(100, 1075), 0, world);

      const next = smoother.step(at(100, 5), 16, world);

      expect(next.y).toBeGreaterThan(1075);
      expect(next.y).toBeLessThan(1090);
    });
  });

  it("jumps straight to a target that is very far, for example after a respawn", () => {
    const smoother = new CameraSmoother({ jumpDistance: 300 });
    smoother.step(at(100, 100), 0, world);

    const next = smoother.step(at(100, 500), 16, world);

    expect(next).toEqual(at(100, 500));
  });

  it("jumps after a long pause, such as a hidden tab", () => {
    const smoother = new CameraSmoother();
    smoother.step(at(100), 0, world);

    expect(smoother.step(at(300), 10_000, world)).toEqual(at(300));
  });

  it("starts over after a reset", () => {
    const smoother = new CameraSmoother();
    smoother.step(at(100), 0, world);

    smoother.reset();

    expect(smoother.step(at(900), 16, world)).toEqual(at(900));
  });

  it("is slower to start with a larger time constant", () => {
    const quick = new CameraSmoother({ timeConstantMs: 50 });
    const slowly = new CameraSmoother({ timeConstantMs: 400 });
    quick.step(at(100), 0, world);
    slowly.step(at(100), 0, world);

    expect(quick.step(at(200), 16, world).x).toBeGreaterThan(slowly.step(at(200), 16, world).x);
  });

  it("falls behind a target that keeps moving, by a small steady distance", () => {
    const smoother = new CameraSmoother({ timeConstantMs: 100 });
    smoother.step(at(100), 0, world);

    let x = 0;
    for (let t = 8; t <= 2000; t += 8) x = smoother.step(at(100 + t * 0.225), t, world).x;

    // 225 units a second with a 100 ms time constant: about 22 units behind.
    expect(100 + 2000 * 0.225 - x).toBeGreaterThan(18);
    expect(100 + 2000 * 0.225 - x).toBeLessThan(26);
  });
});
