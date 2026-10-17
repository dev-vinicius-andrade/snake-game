export type TrackingState = "tracking" | "lost";

export interface TrackingMonitorOptions {
  /** How long the face must be gone before tracking counts as lost. Short dropouts are forgiven. */
  lostAfterMs?: number;
  /** How long a face must be seen before tracking counts as found. */
  recoverAfterMs?: number;
}

/** Turns per frame face detection into a stable "tracking" or "lost" state. */
export class TrackingMonitor {
  private readonly lostAfterMs: number;
  private readonly recoverAfterMs: number;
  private current: TrackingState = "lost";
  private presentSince: number | null = null;
  private absentSince: number | null = null;

  constructor({ lostAfterMs = 600, recoverAfterMs = 150 }: TrackingMonitorOptions = {}) {
    if (!(lostAfterMs >= 0)) throw new RangeError("lostAfterMs must not be negative");
    if (!(recoverAfterMs >= 0)) throw new RangeError("recoverAfterMs must not be negative");
    this.lostAfterMs = lostAfterMs;
    this.recoverAfterMs = recoverAfterMs;
  }

  get state(): TrackingState {
    return this.current;
  }

  /** The new state when it changed, otherwise null. */
  update(facePresent: boolean, timestampMs: number): TrackingState | null {
    return facePresent ? this.seen(timestampMs) : this.missing(timestampMs);
  }

  reset() {
    this.current = "lost";
    this.presentSince = null;
    this.absentSince = null;
  }

  private seen(timestampMs: number): TrackingState | null {
    this.absentSince = null;
    this.presentSince ??= timestampMs;
    if (this.current === "tracking" || timestampMs - this.presentSince < this.recoverAfterMs) return null;
    this.current = "tracking";
    return "tracking";
  }

  private missing(timestampMs: number): TrackingState | null {
    this.presentSince = null;
    this.absentSince ??= timestampMs;
    if (this.current === "lost" || timestampMs - this.absentSince < this.lostAfterMs) return null;
    this.current = "lost";
    return "lost";
  }
}
