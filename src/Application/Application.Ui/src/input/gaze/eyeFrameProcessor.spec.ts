import { describe, expect, it } from "vitest";
import type { BlendshapeScore } from "./blendshapes";
import type { Calibration } from "./calibration";
import { EyeFrameProcessor } from "./eyeFrameProcessor";

const NAMES = [
  "eyeLookInLeft", "eyeLookOutLeft", "eyeLookUpLeft", "eyeLookDownLeft",
  "eyeLookInRight", "eyeLookOutRight", "eyeLookUpRight", "eyeLookDownRight",
  "eyeBlinkLeft", "eyeBlinkRight",
];

function shapes(overrides: Record<string, number> = {}): BlendshapeScore[] {
  return NAMES.map((categoryName) => ({ categoryName, score: overrides[categoryName] ?? 0 }));
}

const lookRight = (amount: number) => ({ eyeLookOutRight: amount, eyeLookInLeft: amount });
const blink = (amount: number) => ({ eyeBlinkLeft: amount, eyeBlinkRight: amount });

describe("EyeFrameProcessor", () => {
  it("maps a neutral face to the middle of the stage", () => {
    const sample = new EyeFrameProcessor().process({ t: 10, blendshapes: shapes() });

    expect(sample).toMatchObject({ t: 10, x: 0.5, y: 0.5, present: true, blink: 0, raw: { x: 0, y: 0 } });
  });

  it("applies the default calibration to the eye signal", () => {
    const sample = new EyeFrameProcessor().process({ t: 0, blendshapes: shapes(lookRight(1)) });

    expect(sample.x).toBeCloseTo(1, 9);
    expect(sample.raw).toEqual({ x: 1, y: 0 });
  });

  it("applies a custom calibration", () => {
    const calibration: Calibration = { x: { offset: 0.5, gain: 0.25 }, y: { offset: 0.5, gain: 0.25 } };
    const processor = new EyeFrameProcessor(calibration);

    expect(processor.process({ t: 0, blendshapes: shapes(lookRight(1)) }).x).toBeCloseTo(0.75, 9);
  });

  it("switches calibration on the fly", () => {
    const processor = new EyeFrameProcessor();
    processor.setCalibration({ x: { offset: 0.5, gain: 0.1 }, y: { offset: 0.5, gain: 0.1 } });

    expect(processor.process({ t: 0, blendshapes: shapes(lookRight(1)) }).x).toBeCloseTo(0.6, 9);
  });

  it("passes the blink score through", () => {
    const sample = new EyeFrameProcessor().process({ t: 0, blendshapes: shapes(blink(0.8)) });

    expect(sample.blink).toBeCloseTo(0.8, 9);
  });

  it("holds the last gaze while the eyes are closing, because blinking disturbs the look blendshapes", () => {
    const processor = new EyeFrameProcessor();
    processor.process({ t: 0, blendshapes: shapes(lookRight(0.6)) });

    const sample = processor.process({ t: 16, blendshapes: shapes({ ...blink(0.9), eyeLookDownLeft: 0.9, eyeLookDownRight: 0.9 }) });

    expect(sample.raw).toEqual({ x: 0.6, y: 0 });
    expect(sample.blink).toBeCloseTo(0.9, 9);
  });

  it("follows the gaze again once the eyes reopen", () => {
    const processor = new EyeFrameProcessor();
    processor.process({ t: 0, blendshapes: shapes(lookRight(0.6)) });
    processor.process({ t: 16, blendshapes: shapes(blink(0.9)) });

    const sample = processor.process({ t: 32, blendshapes: shapes(lookRight(0.2)) });

    expect(sample.raw!.x).toBeCloseTo(0.2, 9);
  });

  it("reports no face when there are no blendshapes", () => {
    const sample = new EyeFrameProcessor().process({ t: 5, blendshapes: null });

    expect(sample).toMatchObject({ t: 5, present: false });
  });

  it("reports no face when the blendshapes are incomplete", () => {
    const sample = new EyeFrameProcessor().process({ t: 5, blendshapes: [{ categoryName: "jawOpen", score: 1 }] });

    expect(sample.present).toBe(false);
  });

  it("keeps the last position in a no-face sample so the gaze does not jump", () => {
    const processor = new EyeFrameProcessor();
    const seen = processor.process({ t: 0, blendshapes: shapes(lookRight(1)) });

    const lost = processor.process({ t: 16, blendshapes: null });

    expect(lost.x).toBe(seen.x);
    expect(lost.y).toBe(seen.y);
  });

  it("uses the stage centre for a no-face sample before any face was seen", () => {
    const lost = new EyeFrameProcessor().process({ t: 0, blendshapes: null });

    expect([lost.x, lost.y]).toEqual([0.5, 0.5]);
  });

  it("does not report a blink for a no-face sample", () => {
    expect(new EyeFrameProcessor().process({ t: 0, blendshapes: null }).blink).toBeUndefined();
  });
});
