import { describe, expect, it } from "vitest";
import {
  CALIBRATION_TARGETS,
  CalibrationRecorder,
  applyCalibration,
  defaultCalibration,
  fitAxis,
  isUsableCalibration,
  parseCalibration,
  type Calibration,
} from "./calibration";

/** Eye signal a user would produce when looking at a stage point, for a made up linear eye. */
const eye = (gainX: number, offsetX: number, gainY: number, offsetY: number) => (stage: { x: number; y: number }) => ({
  x: (stage.x - offsetX) / gainX,
  y: (stage.y - offsetY) / gainY,
});

function record(recorder: CalibrationRecorder, signalFor: (stage: { x: number; y: number }) => { x: number; y: number }) {
  let guard = 0;
  while (recorder.target && guard++ < 10_000) {
    const target = CALIBRATION_TARGETS.find((candidate) => candidate.id === recorder.target)!;
    recorder.add(signalFor(target));
  }
  return recorder.result();
}

describe("fitAxis", () => {
  it("recovers an exact line", () => {
    const fit = fitAxis([
      { signal: -1, target: 0.1 },
      { signal: 0, target: 0.5 },
      { signal: 1, target: 0.9 },
    ])!;

    expect(fit.gain).toBeCloseTo(0.4, 9);
    expect(fit.offset).toBeCloseTo(0.5, 9);
  });

  it("recovers a negative gain, which a flipped camera would give", () => {
    const fit = fitAxis([
      { signal: 1, target: 0.1 },
      { signal: 0, target: 0.5 },
      { signal: -1, target: 0.9 },
    ])!;

    expect(fit.gain).toBeCloseTo(-0.4, 9);
  });

  it("finds the least squares line through noisy points", () => {
    const fit = fitAxis([
      { signal: -1, target: 0.15 },
      { signal: 0, target: 0.5 },
      { signal: 1, target: 0.85 },
      { signal: 0, target: 0.52 },
    ])!;

    expect(fit.gain).toBeCloseTo(0.35, 2);
  });

  it("needs two different signals", () => {
    expect(fitAxis([{ signal: 0.3, target: 0.5 }])).toBeNull();
    expect(fitAxis([{ signal: 0.3, target: 0.2 }, { signal: 0.3, target: 0.8 }])).toBeNull();
    expect(fitAxis([])).toBeNull();
  });
});

describe("applyCalibration", () => {
  it("maps a neutral signal to the stage centre by default", () => {
    expect(applyCalibration(defaultCalibration, { x: 0, y: 0 })).toEqual({ x: 0.5, y: 0.5 });
  });

  it("maps the extremes of the default calibration to the stage edges", () => {
    expect(applyCalibration(defaultCalibration, { x: -1, y: 1 })).toEqual({ x: 0, y: 1 });
  });

  it("uses the fitted offset and gain per axis", () => {
    const calibration: Calibration = { x: { offset: 0.4, gain: 0.8 }, y: { offset: 0.6, gain: -0.5 } };

    const stage = applyCalibration(calibration, { x: 0.5, y: 0.2 });

    expect(stage.x).toBeCloseTo(0.8, 9);
    expect(stage.y).toBeCloseTo(0.5, 9);
  });

  it("keeps results within a quarter stage of the edges", () => {
    const wild = applyCalibration({ x: { offset: 0.5, gain: 50 }, y: { offset: 0.5, gain: -50 } }, { x: 1, y: 1 });

    expect(wild).toEqual({ x: 1.25, y: -0.25 });
  });
});

describe("isUsableCalibration", () => {
  it("accepts the default", () => {
    expect(isUsableCalibration(defaultCalibration)).toBe(true);
  });

  it.each([
    { x: { offset: 0.5, gain: 0.01 }, y: { offset: 0.5, gain: 0.5 } },
    { x: { offset: 0.5, gain: 0.5 }, y: { offset: 0.5, gain: 100 } },
    { x: { offset: Number.NaN, gain: 0.5 }, y: { offset: 0.5, gain: 0.5 } },
    { x: { offset: 0.5, gain: Number.POSITIVE_INFINITY }, y: { offset: 0.5, gain: 0.5 } },
  ])("rejects %j", (calibration) => {
    expect(isUsableCalibration(calibration)).toBe(false);
  });
});

describe("parseCalibration", () => {
  it("round trips through JSON", () => {
    const calibration: Calibration = { x: { offset: 0.45, gain: 0.7 }, y: { offset: 0.55, gain: 0.6 } };

    expect(parseCalibration(JSON.parse(JSON.stringify(calibration)))).toEqual(calibration);
  });

  it.each([null, undefined, 5, "x", {}, { x: { offset: 1 } }, { x: { offset: "a", gain: 1 }, y: { offset: 0, gain: 1 } }])(
    "returns null for %j",
    (raw) => {
      expect(parseCalibration(raw)).toBeNull();
    }
  );

  it("returns null for a calibration that is not usable", () => {
    expect(parseCalibration({ x: { offset: 0.5, gain: 0 }, y: { offset: 0.5, gain: 1 } })).toBeNull();
  });
});

describe("CalibrationRecorder", () => {
  it("asks for the five targets in order", () => {
    const recorder = new CalibrationRecorder({ framesPerTarget: 3, settleFrames: 0 });
    const seen: string[] = [];
    while (recorder.target) {
      seen.push(recorder.target);
      for (let frame = 0; frame < 3; frame++) recorder.add({ x: 0, y: 0 });
    }

    expect(seen).toEqual(["center", "left", "right", "up", "down"]);
  });

  it("recovers the eye model it was fed", () => {
    const recorder = new CalibrationRecorder({ framesPerTarget: 5, settleFrames: 2 });

    const calibration = record(recorder, eye(0.8, 0.5, 0.6, 0.5))!;

    expect(calibration.x.gain).toBeCloseTo(0.8, 6);
    expect(calibration.x.offset).toBeCloseTo(0.5, 6);
    expect(calibration.y.gain).toBeCloseTo(0.6, 6);
    expect(recorder.complete).toBe(true);
    expect(recorder.target).toBeNull();
  });

  it("copes with a flipped horizontal axis", () => {
    const recorder = new CalibrationRecorder({ framesPerTarget: 4, settleFrames: 0 });

    const calibration = record(recorder, eye(-0.8, 0.5, 0.6, 0.5))!;

    expect(calibration.x.gain).toBeCloseTo(-0.8, 6);
  });

  it("ignores the settling frames after each target change", () => {
    const recorder = new CalibrationRecorder({ framesPerTarget: 4, settleFrames: 3 });
    const model = eye(0.8, 0.5, 0.6, 0.5);
    let frame = 0;
    while (recorder.target) {
      const target = CALIBRATION_TARGETS.find((candidate) => candidate.id === recorder.target)!;
      const settling = frame % 7 < 3;
      recorder.add(settling ? { x: 5, y: -5 } : model(target));
      frame++;
    }

    expect(recorder.result()!.x.gain).toBeCloseTo(0.8, 6);
  });

  it("takes the median of a target's frames so a blink or outlier does not skew it", () => {
    const recorder = new CalibrationRecorder({ framesPerTarget: 5, settleFrames: 0 });
    const model = eye(0.8, 0.5, 0.6, 0.5);
    let frame = 0;
    while (recorder.target) {
      const target = CALIBRATION_TARGETS.find((candidate) => candidate.id === recorder.target)!;
      recorder.add(frame % 5 === 2 ? { x: 9, y: 9 } : model(target));
      frame++;
    }

    expect(recorder.result()!.x.gain).toBeCloseTo(0.8, 6);
  });

  it("reports progress from 0 to 1", () => {
    const recorder = new CalibrationRecorder({ framesPerTarget: 2, settleFrames: 0 });
    expect(recorder.progress).toBe(0);

    recorder.add({ x: 0, y: 0 });
    recorder.add({ x: 0, y: 0 });
    expect(recorder.progress).toBeCloseTo(1 / 5, 9);

    record(recorder, eye(0.8, 0.5, 0.6, 0.5));
    expect(recorder.progress).toBe(1);
  });

  it("has no result until every target was recorded", () => {
    const recorder = new CalibrationRecorder({ framesPerTarget: 2, settleFrames: 0 });
    recorder.add({ x: 0, y: 0 });

    expect(recorder.result()).toBeNull();
    expect(recorder.complete).toBe(false);
  });

  it("gives no result when the eyes never moved", () => {
    const recorder = new CalibrationRecorder({ framesPerTarget: 3, settleFrames: 0 });

    expect(record(recorder, () => ({ x: 0.1, y: 0.1 }))).toBeNull();
    expect(recorder.complete).toBe(true);
  });

  it("gives no result when the eyes barely moved", () => {
    const recorder = new CalibrationRecorder({ framesPerTarget: 3, settleFrames: 0 });

    expect(record(recorder, eye(40, 0.5, 40, 0.5))).toBeNull();
  });

  it("ignores samples after it is complete", () => {
    const recorder = new CalibrationRecorder({ framesPerTarget: 2, settleFrames: 0 });
    const calibration = record(recorder, eye(0.8, 0.5, 0.6, 0.5));

    recorder.add({ x: 100, y: 100 });

    expect(recorder.result()).toEqual(calibration);
  });

  it("starts over after reset", () => {
    const recorder = new CalibrationRecorder({ framesPerTarget: 2, settleFrames: 0 });
    record(recorder, eye(0.8, 0.5, 0.6, 0.5));

    recorder.reset();

    expect(recorder.target).toBe("center");
    expect(recorder.progress).toBe(0);
    expect(recorder.result()).toBeNull();
  });

  it.each([{ framesPerTarget: 0 }, { framesPerTarget: 3.5 }, { settleFrames: -1 }])("rejects the options %j", (options) => {
    expect(() => new CalibrationRecorder(options)).toThrow(RangeError);
  });
});

describe("CALIBRATION_TARGETS", () => {
  it("covers the centre and the four directions inside the stage", () => {
    expect(CALIBRATION_TARGETS.map((target) => target.id)).toEqual(["center", "left", "right", "up", "down"]);
    CALIBRATION_TARGETS.forEach((target) => {
      expect(target.x).toBeGreaterThan(0);
      expect(target.x).toBeLessThan(1);
      expect(target.y).toBeGreaterThan(0);
      expect(target.y).toBeLessThan(1);
    });
  });
});
