import type { GamePoint } from "@/types/game";

/** Stage coordinate = offset + gain * eye signal, for one axis. Gain can be negative. */
export interface AxisFit {
  offset: number;
  gain: number;
}

export interface Calibration {
  x: AxisFit;
  y: AxisFit;
}

/** Without a calibration a signal of -1..1 spreads over the whole stage. */
export const defaultCalibration: Calibration = { x: { offset: 0.5, gain: 0.5 }, y: { offset: 0.5, gain: 0.5 } };

const MIN_GAIN = 0.15;
const MAX_GAIN = 20;
const STAGE_MARGIN = 0.25;

export type CalibrationTargetId = "center" | "left" | "right" | "up" | "down";

export interface CalibrationTarget {
  id: CalibrationTargetId;
  /** Where the dot is shown, in stage coordinates. */
  x: number;
  y: number;
}

export const CALIBRATION_TARGETS: readonly CalibrationTarget[] = [
  { id: "center", x: 0.5, y: 0.5 },
  { id: "left", x: 0.12, y: 0.5 },
  { id: "right", x: 0.88, y: 0.5 },
  { id: "up", x: 0.5, y: 0.12 },
  { id: "down", x: 0.5, y: 0.88 },
];

/** Least squares line through (signal, target) points. Null when the signal never changes. */
export function fitAxis(points: ReadonlyArray<{ signal: number; target: number }>): AxisFit | null {
  if (points.length < 2) return null;
  const count = points.length;
  const meanSignal = points.reduce((sum, point) => sum + point.signal, 0) / count;
  const meanTarget = points.reduce((sum, point) => sum + point.target, 0) / count;
  const spread = points.reduce((sum, point) => sum + (point.signal - meanSignal) ** 2, 0);
  if (!(spread > 1e-12)) return null;

  const covariance = points.reduce((sum, point) => sum + (point.signal - meanSignal) * (point.target - meanTarget), 0);
  const gain = covariance / spread;
  return { gain, offset: meanTarget - gain * meanSignal };
}

const clampToStage = (value: number) => Math.min(1 + STAGE_MARGIN, Math.max(-STAGE_MARGIN, value));

/** Eye signal to stage coordinates, kept within a quarter stage of the edges. */
export function applyCalibration(calibration: Calibration, signal: { x: number; y: number }): GamePoint {
  return {
    x: clampToStage(calibration.x.offset + calibration.x.gain * signal.x),
    y: clampToStage(calibration.y.offset + calibration.y.gain * signal.y),
  };
}

const isUsableAxis = ({ offset, gain }: AxisFit) =>
  Number.isFinite(offset) && Number.isFinite(gain) && Math.abs(gain) >= MIN_GAIN && Math.abs(gain) <= MAX_GAIN;

export const isUsableCalibration = (calibration: Calibration) => isUsableAxis(calibration.x) && isUsableAxis(calibration.y);

function parseAxis(raw: unknown): AxisFit | null {
  if (typeof raw !== "object" || raw === null) return null;
  const { offset, gain } = raw as Record<string, unknown>;
  return typeof offset === "number" && typeof gain === "number" ? { offset, gain } : null;
}

/** For values read back from storage: a usable calibration or null. */
export function parseCalibration(raw: unknown): Calibration | null {
  if (typeof raw !== "object" || raw === null) return null;
  const x = parseAxis((raw as Record<string, unknown>).x);
  const y = parseAxis((raw as Record<string, unknown>).y);
  const calibration = x && y ? { x, y } : null;
  return calibration && isUsableCalibration(calibration) ? calibration : null;
}

export interface CalibrationRecorderOptions {
  /** Frames averaged per target. */
  framesPerTarget?: number;
  /** Frames dropped right after a target appears, while the eyes are still moving there. */
  settleFrames?: number;
}

const median = (values: number[]) => {
  const sorted = [...values].sort((a, b) => a - b);
  const middle = Math.floor(sorted.length / 2);
  return sorted.length % 2 === 0 ? (sorted[middle - 1] + sorted[middle]) / 2 : sorted[middle];
};

/**
 * Walks through the calibration targets while the user looks at each one, and fits the eye signal to the stage.
 * Frames per target are reduced with the median, so a stray blink does not skew the result.
 */
export class CalibrationRecorder {
  private readonly framesPerTarget: number;
  private readonly settleFrames: number;
  private index = 0;
  private skipped = 0;
  private frames: Array<{ x: number; y: number }> = [];
  private readonly recorded: Array<{ x: number; y: number }> = [];

  constructor({ framesPerTarget = 30, settleFrames = 8 }: CalibrationRecorderOptions = {}) {
    if (!Number.isInteger(framesPerTarget) || framesPerTarget < 1) throw new RangeError("framesPerTarget must be a whole number of at least 1");
    if (!Number.isInteger(settleFrames) || settleFrames < 0) throw new RangeError("settleFrames must be a whole number, 0 or more");
    this.framesPerTarget = framesPerTarget;
    this.settleFrames = settleFrames;
  }

  /** The target to look at now, or null once every target was recorded. */
  get target(): CalibrationTargetId | null {
    return this.index < CALIBRATION_TARGETS.length ? CALIBRATION_TARGETS[this.index].id : null;
  }

  get complete(): boolean {
    return this.index >= CALIBRATION_TARGETS.length;
  }

  /** 0 to 1 over all targets. */
  get progress(): number {
    if (this.complete) return 1;
    return (this.index + this.frames.length / this.framesPerTarget) / CALIBRATION_TARGETS.length;
  }

  add(signal: { x: number; y: number }) {
    if (this.complete) return;
    if (this.skipped < this.settleFrames) {
      this.skipped++;
      return;
    }
    this.frames.push(signal);
    if (this.frames.length < this.framesPerTarget) return;

    this.recorded.push({ x: median(this.frames.map((frame) => frame.x)), y: median(this.frames.map((frame) => frame.y)) });
    this.frames = [];
    this.skipped = 0;
    this.index++;
  }

  /** The fitted calibration, or null while unfinished or when the eyes did not move enough to fit. */
  result(): Calibration | null {
    if (!this.complete) return null;
    const at = (id: CalibrationTargetId) => this.recorded[CALIBRATION_TARGETS.findIndex((target) => target.id === id)];
    const stage = (id: CalibrationTargetId) => CALIBRATION_TARGETS.find((target) => target.id === id)!;
    const x = fitAxis((["left", "center", "right"] as const).map((id) => ({ signal: at(id).x, target: stage(id).x })));
    const y = fitAxis((["up", "center", "down"] as const).map((id) => ({ signal: at(id).y, target: stage(id).y })));
    const calibration = x && y ? { x, y } : null;
    return calibration && isUsableCalibration(calibration) ? calibration : null;
  }

  reset() {
    this.index = 0;
    this.skipped = 0;
    this.frames = [];
    this.recorded.length = 0;
  }
}
