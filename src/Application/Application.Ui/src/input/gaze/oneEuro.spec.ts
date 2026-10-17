import { describe, expect, it } from "vitest";
import { OneEuroFilter, PointFilter } from "./oneEuro";

/** Deterministic noise in [-1, 1). */
function noise(seed: number) {
  let state = seed;
  return () => {
    state = (state * 1664525 + 1013904223) % 4294967296;
    return (state / 4294967296) * 2 - 1;
  };
}

const deviation = (values: number[]) => {
  const mean = values.reduce((sum, value) => sum + value, 0) / values.length;
  return Math.sqrt(values.reduce((sum, value) => sum + (value - mean) ** 2, 0) / values.length);
};

describe("OneEuroFilter", () => {
  it("passes the first sample through untouched", () => {
    expect(new OneEuroFilter().filter(42, 0)).toBe(42);
  });

  it("keeps a constant signal constant", () => {
    const filter = new OneEuroFilter();
    const outputs = Array.from({ length: 30 }, (_, index) => filter.filter(100, index * 16));

    outputs.forEach((value) => expect(value).toBeCloseTo(100, 9));
  });

  it("smooths jitter around a steady value", () => {
    const random = noise(7);
    const inputs = Array.from({ length: 200 }, () => 100 + random() * 20);
    const filter = new OneEuroFilter();
    const outputs = inputs.map((value, index) => filter.filter(value, index * 16.7));

    expect(deviation(outputs.slice(50))).toBeLessThan(deviation(inputs.slice(50)) * 0.5);
  });

  it("follows a jump faster when beta is higher", () => {
    const run = (beta: number) => {
      const filter = new OneEuroFilter({ minCutoff: 1, beta });
      filter.filter(0, 0);
      let output = 0;
      for (let step = 1; step <= 10; step++) output = filter.filter(100, step * 16.7);
      return output;
    };

    expect(run(0.5)).toBeGreaterThan(run(0) + 10);
  });

  it("converges on a new steady value", () => {
    const filter = new OneEuroFilter();
    filter.filter(0, 0);
    let output = 0;
    for (let step = 1; step <= 600; step++) output = filter.filter(100, step * 16.7);

    expect(output).toBeCloseTo(100, 1);
  });

  it("returns the previous output when time does not move forward", () => {
    const filter = new OneEuroFilter();
    filter.filter(10, 100);
    const settled = filter.filter(20, 116);

    expect(filter.filter(500, 116)).toBe(settled);
    expect(filter.filter(500, 50)).toBe(settled);
    expect(Number.isNaN(filter.filter(500, 116))).toBe(false);
  });

  it("starts over after reset", () => {
    const filter = new OneEuroFilter();
    filter.filter(10, 0);
    filter.filter(20, 16);

    filter.reset();

    expect(filter.filter(999, 5000)).toBe(999);
  });

  it.each([
    { minCutoff: 0 },
    { minCutoff: -1 },
    { dCutoff: 0 },
    { beta: -0.1 },
    { minCutoff: Number.NaN },
  ])("rejects the options %j", (options) => {
    expect(() => new OneEuroFilter(options)).toThrow(RangeError);
  });
});

describe("PointFilter", () => {
  it("filters both axes independently", () => {
    const filter = new PointFilter();
    filter.filter({ x: 0, y: 1000 }, 0);

    const output = filter.filter({ x: 100, y: 1000 }, 16.7);

    expect(output.x).toBeGreaterThan(0);
    expect(output.x).toBeLessThan(100);
    expect(output.y).toBeCloseTo(1000, 9);
  });

  it("resets both axes", () => {
    const filter = new PointFilter();
    filter.filter({ x: 1, y: 2 }, 0);
    filter.reset();

    expect(filter.filter({ x: 50, y: 60 }, 10)).toEqual({ x: 50, y: 60 });
  });
});
