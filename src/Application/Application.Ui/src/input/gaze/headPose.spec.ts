import { describe, expect, it } from "vitest";
import { extractHeadSignals } from "./headPose";

/** A column-major 4x4 matrix whose third column, the direction the face points, is (fx, fy, fz). */
const matrixFacing = (fx: number, fy: number, fz: number, translation = [0, 0, 0]) => ({
  rows: 4,
  columns: 4,
  data: [1, 0, 0, 0, 0, 1, 0, 0, fx, fy, fz, 0, translation[0], translation[1], translation[2], 1],
});

describe("extractHeadSignals", () => {
  it("is neutral when the face points straight at the camera", () => {
    expect(extractHeadSignals(matrixFacing(0, 0, 1))).toEqual({ x: 0, y: 0 });
  });

  it("turning to one side gives x as the turn angle in radians", () => {
    const signals = extractHeadSignals(matrixFacing(Math.sin(0.3), 0, Math.cos(0.3)))!;

    expect(signals.x).toBeCloseTo(0.3, 9);
    expect(signals.y).toBeCloseTo(0, 9);
  });

  it("turning the other way gives the opposite sign", () => {
    expect(extractHeadSignals(matrixFacing(-Math.sin(0.3), 0, Math.cos(0.3)))!.x).toBeCloseTo(-0.3, 9);
  });

  it("nodding gives y as the tilt angle in radians", () => {
    const signals = extractHeadSignals(matrixFacing(0, Math.sin(0.2), Math.cos(0.2)))!;

    expect(signals.x).toBeCloseTo(0, 9);
    expect(signals.y).toBeCloseTo(0.2, 9);
  });

  it("combines a turn and a nod", () => {
    const signals = extractHeadSignals(matrixFacing(0.2, 0.1, 0.97))!;

    expect(signals.x).toBeGreaterThan(0);
    expect(signals.y).toBeGreaterThan(0);
  });

  it("does not care how long the direction vector is", () => {
    const short = extractHeadSignals(matrixFacing(Math.sin(0.3), 0, Math.cos(0.3)))!;
    const long = extractHeadSignals(matrixFacing(5 * Math.sin(0.3), 0, 5 * Math.cos(0.3)))!;

    expect(long.x).toBeCloseTo(short.x, 9);
  });

  it("ignores where the head is, only where it points", () => {
    const here = extractHeadSignals(matrixFacing(0.1, 0, 0.99, [0, 0, 0]))!;
    const there = extractHeadSignals(matrixFacing(0.1, 0, 0.99, [50, -30, 400]))!;

    expect(there).toEqual(here);
  });

  it("works for a sideways turn of a quarter circle", () => {
    expect(extractHeadSignals(matrixFacing(1, 0, 0))!.x).toBeCloseTo(Math.PI / 2, 9);
  });

  it("accepts a typed array", () => {
    const { data } = matrixFacing(0, 0, 1);

    expect(extractHeadSignals({ data: new Float32Array(data) })).toEqual({ x: 0, y: 0 });
  });

  it.each([
    ["nothing", null],
    ["undefined", undefined],
    ["too short", { data: [1, 0, 0] }],
    ["not a number", { data: [1, 0, 0, 0, 0, 1, 0, 0, Number.NaN, 0, 1, 0, 0, 0, 0, 1] }],
    ["no direction", { data: [1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1] }],
  ])("returns null for %s", (_name, matrix) => {
    expect(extractHeadSignals(matrix as never)).toBeNull();
  });
});
