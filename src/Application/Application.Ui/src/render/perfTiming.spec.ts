import { afterEach, describe, expect, it, vi } from "vitest";
import { markArrival, sinceArrival, timed } from "./perfTiming";

function meterOnWindow() {
  const component = vi.fn();
  vi.stubGlobal("window", { __snakePerf: { component } });
  return component;
}

describe("perfTiming", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("runs the work and hands back its result when nothing is measuring", () => {
    vi.stubGlobal("window", {});

    expect(timed("work", () => 42)).toBe(42);
  });

  it("works without a window at all", () => {
    expect(timed("work", () => "done")).toBe("done");
    expect(() => sinceArrival("point")).not.toThrow();
  });

  it("tells the meter how long the work took, under its name", () => {
    const component = meterOnWindow();

    timed("work", () => 1);

    expect(component).toHaveBeenCalledTimes(1);
    expect(component.mock.calls[0][1]).toBe("work");
    expect(component.mock.calls[0][2]).toBeGreaterThanOrEqual(0);
  });

  it("still tells the meter when the work throws", () => {
    const component = meterOnWindow();

    expect(() => timed("work", () => { throw new Error("boom"); })).toThrow("boom");

    expect(component).toHaveBeenCalledTimes(1);
  });

  it("tells the meter how long after the last arrival a point was reached", () => {
    const component = meterOnWindow();
    markArrival(performance.now() - 20);

    sinceArrival("point");

    expect(component.mock.calls[0][1]).toBe("point");
    expect(component.mock.calls[0][2]).toBeGreaterThanOrEqual(20);
  });
});
