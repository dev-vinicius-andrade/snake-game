import type { GamePoint } from "@/types/game";

export interface OneEuroOptions {
  /** Cutoff in Hz while the signal is still. Lower smooths more but lags more. */
  minCutoff?: number;
  /** How much the cutoff rises with speed. Higher follows fast moves with less lag. */
  beta?: number;
  /** Cutoff in Hz of the speed estimate. */
  dCutoff?: number;
}

const smoothingFactor = (cutoffHz: number, dtSeconds: number) => 1 / (1 + 1 / (2 * Math.PI * cutoffHz) / dtSeconds);
const lowPass = (value: number, previous: number, alpha: number) => alpha * value + (1 - alpha) * previous;

/**
 * One Euro filter (Casiez, Roussel, Vogel 2012): smooths hard while the signal is still and
 * relaxes while it moves, which suits noisy gaze and pointer input.
 */
export class OneEuroFilter {
  private readonly minCutoff: number;
  private readonly beta: number;
  private readonly dCutoff: number;
  private last: { time: number; raw: number; filtered: number; derivative: number } | null = null;

  constructor({ minCutoff = 1, beta = 0.007, dCutoff = 1 }: OneEuroOptions = {}) {
    if (!(minCutoff > 0)) throw new RangeError("minCutoff must be greater than zero");
    if (!(dCutoff > 0)) throw new RangeError("dCutoff must be greater than zero");
    if (!(beta >= 0)) throw new RangeError("beta must not be negative");
    this.minCutoff = minCutoff;
    this.beta = beta;
    this.dCutoff = dCutoff;
  }

  /** `timestampMs` must grow; a sample that is not newer than the last one returns the last output unchanged. */
  filter(value: number, timestampMs: number): number {
    if (!this.last) {
      this.last = { time: timestampMs, raw: value, filtered: value, derivative: 0 };
      return value;
    }
    const dt = (timestampMs - this.last.time) / 1000;
    if (!(dt > 0)) return this.last.filtered;

    const derivative = lowPass((value - this.last.raw) / dt, this.last.derivative, smoothingFactor(this.dCutoff, dt));
    const cutoff = this.minCutoff + this.beta * Math.abs(derivative);
    const filtered = lowPass(value, this.last.filtered, smoothingFactor(cutoff, dt));
    this.last = { time: timestampMs, raw: value, filtered, derivative };
    return filtered;
  }

  reset() {
    this.last = null;
  }
}

/** Two independent One Euro filters, one per axis. */
export class PointFilter {
  private readonly x: OneEuroFilter;
  private readonly y: OneEuroFilter;

  constructor(options: OneEuroOptions = {}) {
    this.x = new OneEuroFilter(options);
    this.y = new OneEuroFilter(options);
  }

  filter(point: GamePoint, timestampMs: number): GamePoint {
    return { x: this.x.filter(point.x, timestampMs), y: this.y.filter(point.y, timestampMs) };
  }

  reset() {
    this.x.reset();
    this.y.reset();
  }
}
