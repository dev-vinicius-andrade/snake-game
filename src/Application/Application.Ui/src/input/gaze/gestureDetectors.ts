export interface GestureOptions {
  /** The gesture counts as made at this score or above. */
  onThreshold?: number;
  /** After it was made, the score must fall to this or below before the gesture counts as released. */
  offThreshold?: number;
}

export interface PressTriggerOptions extends GestureOptions {
  /** How long the gesture must be held before it fires. */
  minMs?: number;
}

export interface HoldGestureOptions extends GestureOptions {
  /** How long the gesture must be held before it turns on. */
  holdMs?: number;
}

const DEFAULT_ON = 0.5;
const DEFAULT_OFF = 0.3;

function checkThresholds(on: number, off: number) {
  if (!(off < on)) throw new RangeError("offThreshold must be below onThreshold");
}

function checkDuration(name: string, value: number) {
  if (!(value >= 0)) throw new RangeError(`${name} must not be negative`);
}

/** Fires once each time a gesture is held for a minimum time. It has to be released before it can fire again. */
export class PressTrigger {
  private readonly on: number;
  private readonly off: number;
  private readonly minMs: number;
  private since: number | null = null;
  private fired = false;

  constructor({ onThreshold = DEFAULT_ON, offThreshold = DEFAULT_OFF, minMs = 100 }: PressTriggerOptions = {}) {
    checkThresholds(onThreshold, offThreshold);
    checkDuration("minMs", minMs);
    this.on = onThreshold;
    this.off = offThreshold;
    this.minMs = minMs;
  }

  /** True on the sample at which the gesture fires. */
  update(score: number, timestampMs: number): boolean {
    if (score <= this.off) this.reset();
    if (this.fired || score < this.on && this.since === null) return false;
    this.since ??= timestampMs;
    if (timestampMs - this.since < this.minMs) return false;
    this.fired = true;
    return true;
  }

  reset() {
    this.since = null;
    this.fired = false;
  }
}

/** A gesture that stays on while it is held: turns on after a hold time, off when it is released. */
export class HoldGesture {
  private readonly on: number;
  private readonly off: number;
  private readonly holdMs: number;
  private since: number | null = null;
  private active = false;

  constructor({ onThreshold = DEFAULT_ON, offThreshold = DEFAULT_OFF, holdMs = 250 }: HoldGestureOptions & { holdMs?: number } = {}) {
    checkThresholds(onThreshold, offThreshold);
    checkDuration("holdMs", holdMs);
    this.on = onThreshold;
    this.off = offThreshold;
    this.holdMs = holdMs;
  }

  /** The new state when it changed, otherwise null. */
  update(score: number, timestampMs: number): boolean | null {
    return this.active ? this.whileActive(score) : this.whileInactive(score, timestampMs);
  }

  /** False when this ended a running hold, otherwise null. */
  reset(): false | null {
    const wasActive = this.active;
    this.active = false;
    this.since = null;
    return wasActive ? false : null;
  }

  private whileActive(score: number): boolean | null {
    if (score > this.off) return null;
    this.active = false;
    this.since = null;
    return false;
  }

  private whileInactive(score: number, timestampMs: number): boolean | null {
    if (score < this.on) {
      this.since = null;
      return null;
    }
    this.since ??= timestampMs;
    if (timestampMs - this.since < this.holdMs) return null;
    this.active = true;
    return true;
  }
}
