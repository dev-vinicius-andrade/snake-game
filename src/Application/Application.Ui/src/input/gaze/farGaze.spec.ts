import { describe, expect, it } from "vitest";
import { FarGazeBoost } from "./farGaze";

const options = { farRadius: 400, holdMs: 400 };

/** Feeds `(distance, ms)` pairs and returns the `[ms, state]` changes reported. */
function run(boost: FarGazeBoost, samples: Array<[number, number]>) {
  return samples.flatMap(([distance, t]) => {
    const change = boost.update(distance, t);
    return change === null ? [] : [[t, change] as [number, boolean]];
  });
}

describe("FarGazeBoost", () => {
  it("stays off while the gaze is near the middle", () => {
    expect(run(new FarGazeBoost(options), [[0, 0], [100, 100], [350, 800]])).toEqual([]);
  });

  it("turns on after the gaze stayed far for the hold time", () => {
    const changes = run(new FarGazeBoost(options), [[450, 0], [450, 200], [450, 400]]);

    expect(changes).toEqual([[400, true]]);
  });

  it("does not turn on before the hold time is over", () => {
    expect(run(new FarGazeBoost(options), [[450, 0], [450, 399]])).toEqual([]);
  });

  it("restarts the hold when the gaze comes back in between", () => {
    const changes = run(new FarGazeBoost(options), [[450, 0], [100, 200], [450, 300], [450, 600], [450, 700]]);

    expect(changes).toEqual([[700, true]]);
  });

  it("turns off once the gaze returns close enough", () => {
    const changes = run(new FarGazeBoost(options), [[450, 0], [450, 400], [100, 500]]);

    expect(changes).toEqual([[400, true], [500, false]]);
  });

  it("keeps boosting inside the hysteresis band", () => {
    const changes = run(new FarGazeBoost({ ...options, releaseRatio: 0.85 }), [[450, 0], [450, 400], [350, 500], [345, 600]]);

    expect(changes).toEqual([[400, true]]);
  });

  it("only reports changes, not every sample", () => {
    const changes = run(new FarGazeBoost(options), [[450, 0], [450, 400], [450, 500], [450, 600]]);

    expect(changes).toHaveLength(1);
  });

  it("can boost again after it turned off", () => {
    const changes = run(new FarGazeBoost(options), [[450, 0], [450, 400], [0, 500], [450, 600], [450, 1000]]);

    expect(changes).toEqual([[400, true], [500, false], [1000, true]]);
  });

  it("reset reports the boost ending when it was on", () => {
    const boost = new FarGazeBoost(options);
    run(boost, [[450, 0], [450, 400]]);

    expect(boost.reset()).toBe(false);
    expect(boost.reset()).toBeNull();
  });

  it("reset forgets a hold that was in progress", () => {
    const boost = new FarGazeBoost(options);
    boost.update(450, 0);

    boost.reset();

    expect(boost.update(450, 400)).toBeNull();
  });

  it.each([
    { farRadius: 0, holdMs: 400 },
    { farRadius: 400, holdMs: -1 },
    { farRadius: 400, holdMs: 400, releaseRatio: 0 },
    { farRadius: 400, holdMs: 400, releaseRatio: 1.2 },
  ])("rejects the options %j", (bad) => {
    expect(() => new FarGazeBoost(bad)).toThrow(RangeError);
  });
});
