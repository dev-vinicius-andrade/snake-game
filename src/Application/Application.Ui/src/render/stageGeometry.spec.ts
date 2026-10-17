import { describe, expect, it } from "vitest";
import { headCenter, pointToStage, stageToWorld } from "./stageGeometry";

const rect = { left: 100, top: 50, width: 800, height: 450 };

describe("pointToStage", () => {
  it("maps the corners to 0 and 1", () => {
    expect(pointToStage(100, 50, rect)).toEqual({ x: 0, y: 0 });
    expect(pointToStage(900, 500, rect)).toEqual({ x: 1, y: 1 });
  });

  it("maps the middle to one half", () => {
    expect(pointToStage(500, 275, rect)).toEqual({ x: 0.5, y: 0.5 });
  });

  it("keeps points outside the stage instead of clamping them", () => {
    expect(pointToStage(0, 0, rect)).toEqual({ x: -0.125, y: -50 / 450 });
    expect(pointToStage(1700, 50, rect)?.x).toBe(2);
  });

  it.each([
    { left: 0, top: 0, width: 0, height: 100 },
    { left: 0, top: 0, width: 100, height: 0 },
    { left: 0, top: 0, width: -5, height: 100 },
  ])("returns null for the degenerate rect %j", (degenerate) => {
    expect(pointToStage(10, 10, degenerate)).toBeNull();
  });
});

describe("stageToWorld", () => {
  it("scales a stage point to world units", () => {
    expect(stageToWorld({ x: 0.5, y: 0.25 }, { width: 1920, height: 1080 })).toEqual({ x: 960, y: 270 });
  });

  it("scales points outside the stage too", () => {
    expect(stageToWorld({ x: -0.1, y: 1.5 }, { width: 1000, height: 1000 })).toEqual({ x: -100, y: 1500 });
  });
});

describe("headCenter", () => {
  it("moves the top-left corner of the head box to its middle", () => {
    expect(headCenter({ x: 100, y: 200 }, 15)).toEqual({ x: 107.5, y: 207.5 });
  });

  it("uses the server head size by default", () => {
    expect(headCenter({ x: 0, y: 0 })).toEqual({ x: 7.5, y: 7.5 });
  });
});
