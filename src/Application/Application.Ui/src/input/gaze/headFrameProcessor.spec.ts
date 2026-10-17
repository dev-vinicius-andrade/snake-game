import { describe, expect, it } from "vitest";
import type { BlendshapeScore } from "./blendshapes";
import type { Calibration } from "./calibration";
import { HeadFrameProcessor, defaultHeadCalibration } from "./headFrameProcessor";

const NAMES = ["jawOpen", "browInnerUp", "browOuterUpLeft", "browOuterUpRight"];
const shapes = (overrides: Record<string, number> = {}): BlendshapeScore[] =>
  NAMES.map((categoryName) => ({ categoryName, score: overrides[categoryName] ?? 0 }));
const facing = (fx: number, fy: number, fz: number) => ({ data: [1, 0, 0, 0, 0, 1, 0, 0, fx, fy, fz, 0, 0, 0, 0, 1] });
const straight = facing(0, 0, 1);
const turned = (angle: number) => facing(Math.sin(angle), 0, Math.cos(angle));

describe("HeadFrameProcessor", () => {
  it("maps a head that points straight ahead to the middle of the stage", () => {
    const sample = new HeadFrameProcessor().process({ t: 10, blendshapes: shapes(), matrix: straight });

    expect(sample).toMatchObject({ t: 10, x: 0.5, y: 0.5, present: true, jaw: 0, brows: 0, raw: { x: 0, y: 0 } });
  });

  it("turns head angle into a stage position with the default calibration", () => {
    const sample = new HeadFrameProcessor().process({ t: 0, blendshapes: shapes(), matrix: turned(0.2) });

    expect(sample.x).toBeCloseTo(0.5 + defaultHeadCalibration.x.gain * 0.2, 9);
    expect(sample.raw!.x).toBeCloseTo(0.2, 9);
  });

  it("uses a custom calibration", () => {
    const calibration: Calibration = { x: { offset: 0.5, gain: -2 }, y: { offset: 0.5, gain: 2 } };
    const sample = new HeadFrameProcessor(calibration).process({ t: 0, blendshapes: shapes(), matrix: turned(0.1) });

    expect(sample.x).toBeCloseTo(0.3, 9);
  });

  it("switches calibration on the fly", () => {
    const processor = new HeadFrameProcessor();
    processor.setCalibration({ x: { offset: 0.5, gain: 1 }, y: { offset: 0.5, gain: 1 } });

    expect(processor.process({ t: 0, blendshapes: shapes(), matrix: turned(0.2) }).x).toBeCloseTo(0.7, 9);
  });

  it("passes the mouth and eyebrow gestures through", () => {
    const sample = new HeadFrameProcessor().process({
      t: 0,
      blendshapes: shapes({ jawOpen: 0.7, browInnerUp: 0.9, browOuterUpLeft: 0.9, browOuterUpRight: 0.9 }),
      matrix: straight,
    });

    expect(sample.jaw).toBeCloseTo(0.7, 9);
    expect(sample.brows).toBeCloseTo(0.9, 9);
  });

  it("does not report a blink, because eyes are not what drives it", () => {
    expect(new HeadFrameProcessor().process({ t: 0, blendshapes: shapes(), matrix: straight }).blink).toBeUndefined();
  });

  it.each([
    ["no blendshapes", null, straight],
    ["no matrix", shapes(), null],
    ["an unreadable matrix", shapes(), { data: [1, 2, 3] }],
    ["missing gesture blendshapes", [{ categoryName: "eyeBlinkLeft", score: 0 }], straight],
  ])("reports no face for %s", (_name, blendshapes, matrix) => {
    const sample = new HeadFrameProcessor().process({ t: 5, blendshapes: blendshapes as never, matrix: matrix as never });

    expect(sample).toMatchObject({ t: 5, present: false });
  });

  it("keeps the last position when the face is lost so the pointer does not jump", () => {
    const processor = new HeadFrameProcessor();
    const seen = processor.process({ t: 0, blendshapes: shapes(), matrix: turned(0.3) });

    const lost = processor.process({ t: 16, blendshapes: null, matrix: null });

    expect([lost.x, lost.y]).toEqual([seen.x, seen.y]);
  });

  it("uses the middle of the stage for a lost face before any face was seen", () => {
    const lost = new HeadFrameProcessor().process({ t: 0, blendshapes: null, matrix: null });

    expect([lost.x, lost.y]).toEqual([0.5, 0.5]);
  });
});
