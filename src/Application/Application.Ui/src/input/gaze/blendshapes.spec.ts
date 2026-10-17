import { describe, expect, it } from "vitest";
import { extractEyeSignals, missingEyeBlendshapes, type BlendshapeScore } from "./blendshapes";

const NAMES = [
  "eyeLookInLeft",
  "eyeLookOutLeft",
  "eyeLookUpLeft",
  "eyeLookDownLeft",
  "eyeLookInRight",
  "eyeLookOutRight",
  "eyeLookUpRight",
  "eyeLookDownRight",
  "eyeBlinkLeft",
  "eyeBlinkRight",
];

/** All eye blendshapes at zero, with the given ones overridden. */
function shapes(overrides: Record<string, number> = {}): BlendshapeScore[] {
  return NAMES.map((categoryName) => ({ categoryName, score: overrides[categoryName] ?? 0 }));
}

describe("extractEyeSignals", () => {
  it("is neutral when nothing is active", () => {
    expect(extractEyeSignals(shapes())).toEqual({ x: 0, y: 0, blink: 0 });
  });

  it("looking to the subject's right is positive x (right eye outward, left eye inward)", () => {
    const signals = extractEyeSignals(shapes({ eyeLookOutRight: 0.8, eyeLookInLeft: 0.6 }))!;

    expect(signals.x).toBeCloseTo(0.7, 9);
  });

  it("looking to the subject's left is negative x (left eye outward, right eye inward)", () => {
    const signals = extractEyeSignals(shapes({ eyeLookOutLeft: 0.8, eyeLookInRight: 0.6 }))!;

    expect(signals.x).toBeCloseTo(-0.7, 9);
  });

  it("looking down is positive y, like screen coordinates", () => {
    const signals = extractEyeSignals(shapes({ eyeLookDownLeft: 0.5, eyeLookDownRight: 0.7 }))!;

    expect(signals.y).toBeCloseTo(0.6, 9);
  });

  it("looking up is negative y", () => {
    const signals = extractEyeSignals(shapes({ eyeLookUpLeft: 0.4, eyeLookUpRight: 0.4 }))!;

    expect(signals.y).toBeCloseTo(-0.4, 9);
  });

  it("opposing looks cancel out", () => {
    const signals = extractEyeSignals(shapes({ eyeLookOutRight: 0.5, eyeLookInLeft: 0.5, eyeLookOutLeft: 0.5, eyeLookInRight: 0.5 }))!;

    expect(signals.x).toBeCloseTo(0, 9);
  });

  it("blink is the average of both eyes", () => {
    const signals = extractEyeSignals(shapes({ eyeBlinkLeft: 0.9, eyeBlinkRight: 0.5 }))!;

    expect(signals.blink).toBeCloseTo(0.7, 9);
  });

  it("ignores blendshapes it does not need", () => {
    const extra: BlendshapeScore[] = [...shapes({ eyeLookOutRight: 1, eyeLookInLeft: 1 }), { categoryName: "jawOpen", score: 1 }];

    expect(extractEyeSignals(extra)!.x).toBe(1);
  });

  it("clamps scores into 0..1 and treats non finite scores as 0", () => {
    const signals = extractEyeSignals(shapes({ eyeLookOutRight: 5, eyeLookInLeft: Number.NaN, eyeLookDownLeft: -2 }))!;

    expect(signals.x).toBeCloseTo(0.5, 9);
    expect(signals.y).toBeCloseTo(0, 9);
  });

  it.each(NAMES)("returns null when %s is missing", (missing) => {
    expect(extractEyeSignals(shapes().filter((shape) => shape.categoryName !== missing))).toBeNull();
  });

  it("returns null for an empty list", () => {
    expect(extractEyeSignals([])).toBeNull();
  });
});

describe("missingEyeBlendshapes", () => {
  it("is empty when every eye blendshape is there", () => {
    expect(missingEyeBlendshapes(shapes())).toEqual([]);
  });

  it("lists what is missing", () => {
    const partial = shapes().filter((shape) => !["eyeBlinkLeft", "eyeLookUpRight"].includes(shape.categoryName));

    expect(missingEyeBlendshapes(partial)).toEqual(["eyeLookUpRight", "eyeBlinkLeft"].sort((a, b) => NAMES.indexOf(a) - NAMES.indexOf(b)));
  });

  it("lists everything for an empty result", () => {
    expect(missingEyeBlendshapes([])).toEqual(NAMES);
  });
});
