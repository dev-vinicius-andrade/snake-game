import { extractEyeSignals, type BlendshapeScore } from "./blendshapes";
import { applyCalibration, defaultCalibration, type Calibration } from "./calibration";
import type { GazeSample } from "./gazeSource";

/** One camera frame: the face blendshapes, or null when no face was found. */
export interface EyeFrame {
  t: number;
  blendshapes: readonly BlendshapeScore[] | null;
}

/** Eye blink score from which the look blendshapes are no longer trusted. */
const BLINK_FREEZE = 0.5;

/** Turns camera frames into gaze samples: eye signals, calibration, and blink handling. */
export class EyeFrameProcessor {
  private calibration: Calibration;
  private lastRaw: { x: number; y: number } | null = null;
  private lastStage = { x: 0.5, y: 0.5 };

  constructor(calibration: Calibration = defaultCalibration) {
    this.calibration = calibration;
  }

  setCalibration(calibration: Calibration) {
    this.calibration = calibration;
  }

  process(frame: EyeFrame): GazeSample {
    const signals = frame.blendshapes ? extractEyeSignals(frame.blendshapes) : null;
    if (!signals) return { t: frame.t, x: this.lastStage.x, y: this.lastStage.y, present: false };

    // Shutting the eyes moves the look blendshapes, so hold the gaze from just before the blink.
    const raw = signals.blink >= BLINK_FREEZE && this.lastRaw ? this.lastRaw : { x: signals.x, y: signals.y };
    this.lastRaw = raw;
    this.lastStage = applyCalibration(this.calibration, raw);
    return { t: frame.t, x: this.lastStage.x, y: this.lastStage.y, blink: signals.blink, present: true, raw };
  }
}
