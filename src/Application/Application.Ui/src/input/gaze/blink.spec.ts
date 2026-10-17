import { describe, expect, it } from "vitest";
import { BlinkDetector } from "./blink";

/** Feeds `(score, ms)` pairs and returns the times at which a deliberate blink was reported. */
function run(detector: BlinkDetector, samples: Array<[number, number]>) {
  return samples.filter(([score, t]) => detector.update(score, t)).map(([, t]) => t);
}

describe("BlinkDetector", () => {
  it("ignores a natural blink", () => {
    const detector = new BlinkDetector();

    expect(run(detector, [[0, 0], [0.9, 100], [0.9, 200], [0.1, 300], [0, 400]])).toEqual([]);
  });

  it("reports a deliberate blink when the eyes open again", () => {
    const detector = new BlinkDetector();

    const fired = run(detector, [[0, 0], [0.9, 100], [0.9, 400], [0.9, 700], [0.1, 800]]);

    expect(fired).toEqual([800]);
  });

  it("does not report while the eyes are still shut", () => {
    const detector = new BlinkDetector();

    expect(run(detector, [[0, 0], [0.9, 100], [0.9, 700], [0.9, 1200]])).toEqual([]);
  });

  it("reports exactly at the minimum duration", () => {
    const detector = new BlinkDetector({ minMs: 500 });

    expect(run(detector, [[0.9, 0], [0.1, 500]])).toEqual([500]);
    expect(run(new BlinkDetector({ minMs: 500 }), [[0.9, 0], [0.1, 499]])).toEqual([]);
  });

  it("ignores eyes shut for too long, such as resting or looking away", () => {
    const detector = new BlinkDetector({ maxMs: 2000 });

    expect(run(detector, [[0.9, 0], [0.9, 1000], [0.1, 2500]])).toEqual([]);
  });

  it("does not split one blink when the score flickers between the thresholds", () => {
    const detector = new BlinkDetector();

    const fired = run(detector, [[0.9, 0], [0.5, 100], [0.9, 200], [0.55, 300], [0.9, 400], [0.1, 700]]);

    expect(fired).toEqual([700]);
  });

  it("reports two separate deliberate blinks", () => {
    const detector = new BlinkDetector();

    const fired = run(detector, [[0.9, 0], [0.1, 600], [0.9, 1000], [0.1, 1700]]);

    expect(fired).toEqual([600, 1700]);
  });

  it("needs the eyes to open before the next blink counts", () => {
    const detector = new BlinkDetector();

    const fired = run(detector, [[0.9, 0], [0.5, 600], [0.9, 700], [0.1, 1300]]);

    expect(fired).toEqual([1300]);
  });

  it("forgets a half finished blink on reset", () => {
    const detector = new BlinkDetector();
    detector.update(0.9, 0);

    detector.reset();

    expect(detector.update(0.1, 800)).toBe(false);
  });

  it("starts closed eyes from the first sample after a reset", () => {
    const detector = new BlinkDetector();
    detector.reset();

    expect(run(detector, [[0.9, 5000], [0.1, 5600]])).toEqual([5600]);
  });

  it.each([
    { closeThreshold: 0.3, openThreshold: 0.5 },
    { closeThreshold: 0, openThreshold: 0 },
    { minMs: -1 },
    { minMs: 1000, maxMs: 500 },
  ])("rejects the options %j", (options) => {
    expect(() => new BlinkDetector(options)).toThrow(RangeError);
  });
});
