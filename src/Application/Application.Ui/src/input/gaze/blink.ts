export interface BlinkOptions {
  /** Blink score at or above which the eyes count as shut. */
  closeThreshold?: number;
  /** Blink score at or below which they count as open again. Lower than the close threshold, so noise does not flicker. */
  openThreshold?: number;
  /** Shortest shut time that counts as deliberate. Natural blinks last well under half a second. */
  minMs?: number;
  /** Longest shut time that still counts. Beyond it the eyes are resting or looking away. */
  maxMs?: number;
}

/** Recognises a deliberate long blink. It is reported when the eyes open again. */
export class BlinkDetector {
  private readonly closeThreshold: number;
  private readonly openThreshold: number;
  private readonly minMs: number;
  private readonly maxMs: number;
  private closedSince: number | null = null;

  constructor({ closeThreshold = 0.6, openThreshold = 0.4, minMs = 500, maxMs = 2000 }: BlinkOptions = {}) {
    if (!(openThreshold > 0 && openThreshold < closeThreshold && closeThreshold <= 1)) {
      throw new RangeError("thresholds must satisfy 0 < openThreshold < closeThreshold <= 1");
    }
    if (!(minMs >= 0 && maxMs >= minMs)) throw new RangeError("durations must satisfy 0 <= minMs <= maxMs");
    this.closeThreshold = closeThreshold;
    this.openThreshold = openThreshold;
    this.minMs = minMs;
    this.maxMs = maxMs;
  }

  /** True when a deliberate blink just finished. */
  update(blinkScore: number, timestampMs: number): boolean {
    if (this.closedSince === null) {
      if (blinkScore >= this.closeThreshold) this.closedSince = timestampMs;
      return false;
    }
    if (blinkScore > this.openThreshold) return false;

    const shutFor = timestampMs - this.closedSince;
    this.closedSince = null;
    return shutFor >= this.minMs && shutFor <= this.maxMs;
  }

  reset() {
    this.closedSince = null;
  }
}
