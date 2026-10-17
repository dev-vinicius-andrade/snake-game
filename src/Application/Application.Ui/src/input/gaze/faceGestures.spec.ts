import { describe, expect, it } from "vitest";
import type { BlendshapeScore } from "./blendshapes";
import { extractFaceGestures, missingFaceGestureBlendshapes } from "./faceGestures";

const NAMES = ["jawOpen", "browInnerUp", "browOuterUpLeft", "browOuterUpRight"];

function shapes(overrides: Record<string, number> = {}): BlendshapeScore[] {
  return NAMES.map((categoryName) => ({ categoryName, score: overrides[categoryName] ?? 0 }));
}

describe("extractFaceGestures", () => {
  it("is neutral with a relaxed face", () => {
    expect(extractFaceGestures(shapes())).toEqual({ jawOpen: 0, brows: 0 });
  });

  it("reads the mouth opening", () => {
    expect(extractFaceGestures(shapes({ jawOpen: 0.8 }))!.jawOpen).toBeCloseTo(0.8, 9);
  });

  it("averages the three eyebrow raisers", () => {
    const gestures = extractFaceGestures(shapes({ browInnerUp: 0.9, browOuterUpLeft: 0.6, browOuterUpRight: 0.3 }))!;

    expect(gestures.brows).toBeCloseTo(0.6, 9);
  });

  it("needs both inner and outer brows to be raised to reach the top", () => {
    expect(extractFaceGestures(shapes({ browInnerUp: 1 }))!.brows).toBeCloseTo(1 / 3, 9);
  });

  it("ignores blendshapes it does not use", () => {
    const extra: BlendshapeScore[] = [...shapes({ jawOpen: 0.5 }), { categoryName: "mouthSmileLeft", score: 1 }];

    expect(extractFaceGestures(extra)!.jawOpen).toBeCloseTo(0.5, 9);
  });

  it("clamps scores into 0..1 and treats non finite scores as 0", () => {
    const gestures = extractFaceGestures(shapes({ jawOpen: 7, browInnerUp: Number.NaN, browOuterUpLeft: -3 }))!;

    expect(gestures.jawOpen).toBe(1);
    expect(gestures.brows).toBe(0);
  });

  it.each(NAMES)("returns null when %s is missing", (missing) => {
    expect(extractFaceGestures(shapes().filter((shape) => shape.categoryName !== missing))).toBeNull();
  });

  it("returns null for an empty list", () => {
    expect(extractFaceGestures([])).toBeNull();
  });
});

describe("missingFaceGestureBlendshapes", () => {
  it("is empty when everything is there", () => {
    expect(missingFaceGestureBlendshapes(shapes())).toEqual([]);
  });

  it("lists what is missing, in the order they are used", () => {
    const partial = shapes().filter((shape) => !["browOuterUpRight", "jawOpen"].includes(shape.categoryName));

    expect(missingFaceGestureBlendshapes(partial)).toEqual(["jawOpen", "browOuterUpRight"]);
  });
});
