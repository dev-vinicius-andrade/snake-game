import type { RoomDimensions } from "@/types/game";
import type { ControlIntent } from "../intents";
import { BlinkDetector, type BlinkOptions } from "./blink";
import { FarGazeBoost } from "./farGaze";
import type { GazeSample } from "./gazeSource";
import { TrackingMonitor, type TrackingMonitorOptions, type TrackingState } from "./trackingMonitor";

export interface EyeActionsOptions {
  tracking?: TrackingMonitorOptions;
  blink?: BlinkOptions;
  farGaze?: {
    /** How long the gaze must stay far before the boost starts. */
    holdMs?: number;
    /** Far means this fraction of the smaller side of the room away from the middle. */
    radiusFraction?: number;
    releaseRatio?: number;
  };
}

export interface EyeActionsResult {
  intents: ControlIntent[];
  /** The new tracking state when it changed with this sample. */
  tracking: TrackingState | null;
}

const DEFAULT_HOLD_MS = 400;
const DEFAULT_RADIUS_FRACTION = 0.4;

/**
 * The eye actions besides steering: a long blink shoots and a gaze held far from the middle boosts. Nothing
 * fires while the face is not tracked, and a boost or half finished blink ends when tracking is lost.
 */
export class EyeActions {
  private readonly monitor: TrackingMonitor;
  private readonly blink: BlinkDetector;
  private readonly holdMs: number;
  private readonly radiusFraction: number;
  private readonly releaseRatio: number | undefined;
  private boost: FarGazeBoost | null = null;
  private boostRadius = 0;

  constructor(options: EyeActionsOptions = {}) {
    this.monitor = new TrackingMonitor(options.tracking);
    this.blink = new BlinkDetector(options.blink);
    this.holdMs = options.farGaze?.holdMs ?? DEFAULT_HOLD_MS;
    this.radiusFraction = options.farGaze?.radiusFraction ?? DEFAULT_RADIUS_FRACTION;
    this.releaseRatio = options.farGaze?.releaseRatio;
  }

  get state(): TrackingState {
    return this.monitor.state;
  }

  // Parameters are not called `room`: the auto-import plugin would mistake it for the default export of types/room.
  process(sample: GazeSample, roomSize: RoomDimensions): EyeActionsResult {
    const present = sample.present ?? true;
    const tracking = this.monitor.update(present, sample.t);
    const intents: ControlIntent[] = tracking === "lost" ? this.endActions() : [];
    if (this.monitor.state !== "tracking" || !present) return { intents, tracking };

    if (sample.blink !== undefined && this.blink.update(sample.blink, sample.t)) intents.push({ type: "shoot" });
    const boost = this.boostFor(roomSize).update(this.distanceFromMiddle(sample, roomSize), sample.t);
    if (boost !== null) intents.push({ type: "boost", active: boost });
    return { intents, tracking };
  }

  /** Ends whatever is running and forgets that a face was seen. */
  reset(): ControlIntent[] {
    const intents = this.endActions();
    this.monitor.reset();
    return intents;
  }

  private endActions(): ControlIntent[] {
    this.blink.reset();
    return this.boost?.reset() === false ? [{ type: "boost", active: false }] : [];
  }

  private distanceFromMiddle(sample: GazeSample, roomSize: RoomDimensions): number {
    return Math.hypot((sample.x - 0.5) * roomSize.width, (sample.y - 0.5) * roomSize.height);
  }

  private boostFor(roomSize: RoomDimensions): FarGazeBoost {
    const farRadius = this.radiusFraction * Math.min(roomSize.width, roomSize.height);
    if (!this.boost || this.boostRadius !== farRadius) {
      this.boost = new FarGazeBoost({ farRadius, holdMs: this.holdMs, releaseRatio: this.releaseRatio });
      this.boostRadius = farRadius;
    }
    return this.boost;
  }
}
