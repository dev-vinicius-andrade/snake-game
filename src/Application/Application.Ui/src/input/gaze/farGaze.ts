export interface FarGazeOptions {
  /** Distance from the middle of the stage, in the unit of the samples, that counts as looking far. */
  farRadius: number;
  /** The boost ends below `farRadius * releaseRatio`, so a gaze on the edge does not flicker. */
  releaseRatio?: number;
  /** How long the gaze must stay far before the boost starts. */
  holdMs: number;
}

/** Boost while the player keeps looking far from the middle of the screen. */
export class FarGazeBoost {
  private readonly farRadius: number;
  private readonly releaseRatio: number;
  private readonly holdMs: number;
  private farSince: number | null = null;
  private active = false;

  constructor({ farRadius, holdMs, releaseRatio = 0.85 }: FarGazeOptions) {
    if (!(farRadius > 0)) throw new RangeError("farRadius must be greater than zero");
    if (!(holdMs >= 0)) throw new RangeError("holdMs must not be negative");
    if (!(releaseRatio > 0 && releaseRatio <= 1)) throw new RangeError("releaseRatio must be in (0, 1]");
    this.farRadius = farRadius;
    this.holdMs = holdMs;
    this.releaseRatio = releaseRatio;
  }

  /** The new boost state when it changed, otherwise null. */
  update(distance: number, timestampMs: number): boolean | null {
    return this.active ? this.whileActive(distance) : this.whileIdle(distance, timestampMs);
  }

  /** Ends a running boost. Returns false when it was on, otherwise null. */
  reset(): boolean | null {
    const wasActive = this.active;
    this.active = false;
    this.farSince = null;
    return wasActive ? false : null;
  }

  private whileIdle(distance: number, timestampMs: number): boolean | null {
    if (distance < this.farRadius) {
      this.farSince = null;
      return null;
    }
    this.farSince ??= timestampMs;
    if (timestampMs - this.farSince < this.holdMs) return null;
    this.active = true;
    return true;
  }

  private whileActive(distance: number): boolean | null {
    if (distance >= this.farRadius * this.releaseRatio) return null;
    this.active = false;
    this.farSince = null;
    return false;
  }
}
