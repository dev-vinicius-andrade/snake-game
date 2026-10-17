import { describe, expect, it } from "vitest";
import { HoldGesture, PressTrigger } from "./gestureDetectors";

const fires = (trigger: PressTrigger, samples: Array<[number, number]>) => samples.filter(([score, t]) => trigger.update(score, t)).map(([, t]) => t);

describe("PressTrigger", () => {
  it("fires once the gesture was held for the minimum time", () => {
    const trigger = new PressTrigger({ onThreshold: 0.5, offThreshold: 0.3, minMs: 120 });

    expect(fires(trigger, [[0.9, 0], [0.9, 60], [0.9, 120]])).toEqual([120]);
  });

  it("does not fire for a flicker shorter than the minimum time", () => {
    const trigger = new PressTrigger({ minMs: 120 });

    expect(fires(trigger, [[0.9, 0], [0.9, 100], [0.1, 110], [0.1, 500]])).toEqual([]);
  });

  it("fires only once while the gesture is held", () => {
    const trigger = new PressTrigger({ minMs: 100 });

    expect(fires(trigger, [[0.9, 0], [0.9, 100], [0.9, 500], [0.9, 2000]])).toEqual([100]);
  });

  it("fires again after the gesture was released and made again", () => {
    const trigger = new PressTrigger({ minMs: 100 });

    expect(fires(trigger, [[0.9, 0], [0.9, 100], [0.1, 200], [0.9, 300], [0.9, 400]])).toEqual([100, 400]);
  });

  it("keeps counting while the score sits between the thresholds", () => {
    const trigger = new PressTrigger({ onThreshold: 0.5, offThreshold: 0.3, minMs: 100 });

    expect(fires(trigger, [[0.9, 0], [0.4, 50], [0.9, 100]])).toEqual([100]);
  });

  it("is not re-armed by a score between the thresholds", () => {
    const trigger = new PressTrigger({ onThreshold: 0.5, offThreshold: 0.3, minMs: 100 });

    expect(fires(trigger, [[0.9, 0], [0.9, 100], [0.4, 200], [0.9, 300], [0.9, 500]])).toEqual([100]);
  });

  it("forgets a press under way on reset", () => {
    const trigger = new PressTrigger({ minMs: 100 });
    trigger.update(0.9, 0);

    trigger.reset();

    expect(trigger.update(0.9, 100)).toBe(false);
  });

  it.each([
    { onThreshold: 0.3, offThreshold: 0.5 },
    { onThreshold: 0, offThreshold: 0 },
    { minMs: -1 },
  ])("rejects the options %j", (options) => {
    expect(() => new PressTrigger(options)).toThrow(RangeError);
  });
});

const changes = (hold: HoldGesture, samples: Array<[number, number]>) =>
  samples.flatMap(([score, t]) => {
    const change = hold.update(score, t);
    return change === null ? [] : [[t, change] as [number, boolean]];
  });

describe("HoldGesture", () => {
  it("turns on after the gesture was held long enough", () => {
    const hold = new HoldGesture({ onThreshold: 0.5, offThreshold: 0.3, holdMs: 250 });

    expect(changes(hold, [[0.9, 0], [0.9, 100], [0.9, 250]])).toEqual([[250, true]]);
  });

  it("does not turn on before the hold time is over", () => {
    expect(changes(new HoldGesture({ holdMs: 250 }), [[0.9, 0], [0.9, 249]])).toEqual([]);
  });

  it("restarts the hold when the gesture drops below the on threshold in between", () => {
    const hold = new HoldGesture({ onThreshold: 0.5, offThreshold: 0.3, holdMs: 250 });

    expect(changes(hold, [[0.9, 0], [0.2, 100], [0.9, 200], [0.9, 450]])).toEqual([[450, true]]);
  });

  it("turns off when the score falls to the off threshold", () => {
    const hold = new HoldGesture({ onThreshold: 0.5, offThreshold: 0.3, holdMs: 100 });

    expect(changes(hold, [[0.9, 0], [0.9, 100], [0.3, 200]])).toEqual([[100, true], [200, false]]);
  });

  it("stays on between the thresholds", () => {
    const hold = new HoldGesture({ onThreshold: 0.5, offThreshold: 0.3, holdMs: 100 });

    expect(changes(hold, [[0.9, 0], [0.9, 100], [0.4, 200], [0.35, 300]])).toEqual([[100, true]]);
  });

  it("only reports changes", () => {
    expect(changes(new HoldGesture({ holdMs: 100 }), [[0.9, 0], [0.9, 100], [0.9, 200], [0.9, 300]])).toHaveLength(1);
  });

  it("can turn on again after it turned off", () => {
    const hold = new HoldGesture({ holdMs: 100 });

    expect(changes(hold, [[0.9, 0], [0.9, 100], [0, 200], [0.9, 300], [0.9, 400]])).toEqual([[100, true], [200, false], [400, true]]);
  });

  it("reset ends a running hold and says so once", () => {
    const hold = new HoldGesture({ holdMs: 100 });
    changes(hold, [[0.9, 0], [0.9, 100]]);

    expect(hold.reset()).toBe(false);
    expect(hold.reset()).toBeNull();
  });

  it.each([
    { onThreshold: 0.3, offThreshold: 0.5 },
    { holdMs: -1 },
  ])("rejects the options %j", (options) => {
    expect(() => new HoldGesture(options)).toThrow(RangeError);
  });
});
