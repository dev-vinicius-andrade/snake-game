import { describe, expect, it } from "vitest";
import { angleDistance } from "./angles";

describe("angleDistance", () => {
  it.each([
    [0, 0, 0],
    [0, 1, 1],
    [1, 0, 1],
    [-1, 1, 2],
    [Math.PI, -Math.PI, 0],
    [3.1, -3.1, 2 * Math.PI - 6.2],
    [0, Math.PI, Math.PI],
    [0, 2 * Math.PI, 0],
    [0.1, 0.1 + 2 * Math.PI * 3, 0],
  ])("distance between %d and %d is %d", (a, b, expected) => {
    expect(angleDistance(a, b)).toBeCloseTo(expected, 9);
  });

  it("never exceeds PI", () => {
    for (let a = -7; a <= 7; a += 0.5) {
      for (let b = -7; b <= 7; b += 0.5) expect(angleDistance(a, b)).toBeLessThanOrEqual(Math.PI + 1e-12);
    }
  });
});
