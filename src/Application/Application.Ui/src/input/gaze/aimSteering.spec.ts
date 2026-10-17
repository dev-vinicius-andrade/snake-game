import { describe, expect, it } from "vitest";
import { aimStep, initialAimState, type AimOptions, type AimState } from "./aimSteering";

const head = { x: 500, y: 500 };
const options: AimOptions = { deadRadius: 100 };

function step(state: AimState, target: { x: number; y: number }, opts: AimOptions = options) {
  return aimStep(state, head, target, opts);
}

describe("aimStep", () => {
  it("does nothing while the target sits inside the dead zone", () => {
    const result = step(initialAimState, { x: 550, y: 520 });

    expect(result.steer).toBeNull();
    expect(result.state.active).toBe(false);
  });

  it.each([
    ["right", { x: 700, y: 500 }, 0],
    ["down", { x: 500, y: 700 }, Math.PI / 2],
    ["up", { x: 500, y: 300 }, -Math.PI / 2],
    ["left", { x: 300, y: 500 }, Math.PI],
    ["down-right", { x: 600, y: 600 }, Math.PI / 4],
  ])("steers %s once the target leaves the dead zone", (_name, target, expected) => {
    const result = step(initialAimState, target);

    expect(result.steer).toBeCloseTo(expected, 9);
    expect(result.state.active).toBe(true);
  });

  it("activates exactly on the dead radius", () => {
    expect(step(initialAimState, { x: 600, y: 500 }).steer).toBe(0);
  });

  it("stays quiet between the release radius and the dead radius when it was not active", () => {
    expect(step(initialAimState, { x: 590, y: 500 }).steer).toBeNull();
  });

  it("keeps steering between the release radius and the dead radius once active", () => {
    const active = step(initialAimState, { x: 700, y: 500 }).state;

    const result = step(active, { x: 590, y: 540 });

    expect(result.state.active).toBe(true);
    expect(result.steer).not.toBeNull();
  });

  it("goes quiet again below the release radius", () => {
    const active = step(initialAimState, { x: 700, y: 500 }).state;

    const result = step(active, { x: 550, y: 500 });

    expect(result.state.active).toBe(false);
    expect(result.steer).toBeNull();
  });

  it("does not repeat the same angle", () => {
    const first = step(initialAimState, { x: 700, y: 500 });

    expect(step(first.state, { x: 800, y: 500 }).steer).toBeNull();
  });

  it("ignores angle changes below the minimum delta", () => {
    const first = step(initialAimState, { x: 700, y: 500 });
    const nearlySame = { x: 700, y: 500 + 200 * Math.tan(0.02) };

    expect(step(first.state, nearlySame).steer).toBeNull();
  });

  it("reports an angle change at or above the minimum delta", () => {
    const first = step(initialAimState, { x: 700, y: 500 });

    const result = step(first.state, { x: 700, y: 500 + 200 * Math.tan(0.2) });

    expect(result.steer).toBeCloseTo(0.2, 9);
  });

  it("treats angles either side of the wrap point as the same", () => {
    const first = step(initialAimState, { x: 300, y: 499 });
    const other = step(first.state, { x: 300, y: 501 });

    expect(first.steer).not.toBeNull();
    expect(other.steer).toBeNull();
  });

  it("reports the angle again after going quiet and coming back", () => {
    const first = step(initialAimState, { x: 700, y: 500 });
    const quiet = step(first.state, { x: 520, y: 500 });

    expect(step(quiet.state, { x: 700, y: 500 }).steer).toBe(0);
  });

  it("honours a custom minimum angle delta", () => {
    const strict: AimOptions = { deadRadius: 100, minAngleDelta: 0.5 };
    const first = step(initialAimState, { x: 700, y: 500 }, strict);

    expect(step(first.state, { x: 700, y: 500 + 200 * Math.tan(0.3) }, strict).steer).toBeNull();
  });

  it("never mutates the previous state", () => {
    const frozen = Object.freeze({ active: false, lastAngle: null });

    expect(() => step(frozen, { x: 700, y: 500 })).not.toThrow();
  });

  it.each([
    { deadRadius: -1 },
    { deadRadius: Number.NaN },
    { deadRadius: 100, releaseRatio: 0 },
    { deadRadius: 100, releaseRatio: 1.5 },
    { deadRadius: 100, minAngleDelta: -0.1 },
  ])("rejects the options %j", (bad) => {
    expect(() => step(initialAimState, { x: 700, y: 500 }, bad)).toThrow(RangeError);
  });
});

describe("aimStep dwell", () => {
  const dwell: AimOptions = { deadRadius: 100, dwellMs: 300 };
  const at = (state: AimState, target: { x: number; y: number }, t: number) => aimStep(state, head, target, dwell, t);
  const right = { x: 700, y: 500 };

  it("does not steer at the first glance outside the dead zone", () => {
    expect(at(initialAimState, right, 0).steer).toBeNull();
  });

  it("steers once the gaze stayed in that direction for the dwell time", () => {
    let state = at(initialAimState, right, 0).state;
    state = at(state, right, 299).state;

    expect(at(state, right, 299).steer).toBeNull();
    expect(at(state, right, 300).steer).toBe(0);
  });

  it("starts the wait over when the gaze moves to another direction", () => {
    let state = at(initialAimState, right, 0).state;
    state = at(state, { x: 500, y: 700 }, 200).state;

    expect(at(state, { x: 500, y: 700 }, 400).steer).toBeNull();
    expect(at(state, { x: 500, y: 700 }, 500).steer).toBeCloseTo(Math.PI / 2, 9);
  });

  it("is not restarted by a little jitter in the direction", () => {
    let state = at(initialAimState, right, 0).state;
    state = at(state, { x: 700, y: 500 + 200 * Math.tan(0.1) }, 150).state;

    expect(at(state, { x: 700, y: 500 - 200 * Math.tan(0.1) }, 300).steer).not.toBeNull();
  });

  it("starts the wait over when the gaze falls back into the dead zone", () => {
    let state = at(initialAimState, right, 0).state;
    state = at(state, { x: 510, y: 500 }, 100).state;
    state = at(state, right, 150).state;

    expect(at(state, right, 400).steer).toBeNull();
    expect(at(state, right, 450).steer).toBe(0);
  });

  it("follows the gaze without waiting again once steering", () => {
    let state = at(initialAimState, right, 0).state;
    state = at(state, right, 300).state;

    expect(at(state, { x: 700, y: 500 + 200 * Math.tan(0.4) }, 316).steer).toBeCloseTo(0.4, 9);
  });

  it("waits again after the gaze went back to the dead zone", () => {
    let state = at(initialAimState, right, 0).state;
    state = at(state, right, 300).state;
    state = at(state, { x: 510, y: 500 }, 400).state;

    expect(at(state, right, 450).steer).toBeNull();
  });

  it("does not wait at all without a dwell time", () => {
    expect(aimStep(initialAimState, head, right, { deadRadius: 100 }, 0).steer).toBe(0);
    expect(aimStep(initialAimState, head, right, { deadRadius: 100, dwellMs: 0 }, 0).steer).toBe(0);
  });

  it.each([{ dwellMs: -1 }, { dwellMs: 300, dwellAngle: 0 }, { dwellMs: 300, dwellAngle: -0.1 }])("rejects the options %j", (bad) => {
    expect(() => aimStep(initialAimState, head, right, { deadRadius: 100, ...bad }, 0)).toThrow(RangeError);
  });
});
