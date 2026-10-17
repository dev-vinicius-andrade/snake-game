import { applyCalibration, type Calibration } from "./calibration";
import { extractFaceGestures } from "./faceGestures";
import type { BlendshapeScore } from "./blendshapes";
import type { GazeSample } from "./gazeSource";
import { extractHeadSignals, type HeadMatrix } from "./headPose";

/** One camera frame in head mode: the face blendshapes and head matrix, or null when no face was found. */
export interface HeadFrame {
  t: number;
  blendshapes: readonly BlendshapeScore[] | null;
  matrix: HeadMatrix | null;
}

/** Without a calibration 0.3 rad (about 17 degrees) of head turn moves the aim by about half the stage. */
export const defaultHeadCalibration: Calibration = { x: { offset: 0.5, gain: 1.6 }, y: { offset: 0.5, gain: 1.6 } };

/** Turns camera frames into gaze samples in head mode: head angle through calibration, plus mouth and eyebrows. */
export class HeadFrameProcessor {
  private calibration: Calibration;
  private lastStage = { x: 0.5, y: 0.5 };

  constructor(calibration: Calibration = defaultHeadCalibration) {
    this.calibration = calibration;
  }

  setCalibration(calibration: Calibration) {
    this.calibration = calibration;
  }

  process(frame: HeadFrame): GazeSample {
    const raw = extractHeadSignals(frame.matrix);
    const gestures = frame.blendshapes ? extractFaceGestures(frame.blendshapes) : null;
    if (!raw || !gestures) return { t: frame.t, x: this.lastStage.x, y: this.lastStage.y, present: false };

    this.lastStage = applyCalibration(this.calibration, raw);
    return { t: frame.t, x: this.lastStage.x, y: this.lastStage.y, present: true, jaw: gestures.jawOpen, brows: gestures.brows, raw };
  }
}
