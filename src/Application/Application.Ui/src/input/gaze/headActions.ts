import type { ControlIntent } from "../intents";
import type { GazeSample } from "./gazeSource";
import { HoldGesture, PressTrigger } from "./gestureDetectors";
import { TrackingMonitor, type TrackingMonitorOptions, type TrackingState } from "./trackingMonitor";

export interface HeadActionsOptions {
  tracking?: TrackingMonitorOptions;
  /** Open mouth: shoots once per opening. */
  shoot?: { onThreshold?: number; offThreshold?: number; minMs?: number };
  /** Raised eyebrows: boost while they stay up. */
  boost?: { onThreshold?: number; offThreshold?: number; holdMs?: number };
}

export interface HeadActionsResult {
  intents: ControlIntent[];
  /** The new tracking state when it changed with this sample. */
  tracking: TrackingState | null;
}

/**
 * The head mode actions besides steering: an open mouth shoots and raised eyebrows boost. Nothing fires while
 * the face is not tracked, and a running boost ends when tracking is lost or the actions stop.
 */
export class HeadActions {
  private readonly monitor: TrackingMonitor;
  private readonly shoot: PressTrigger;
  private readonly boost: HoldGesture;

  constructor(options: HeadActionsOptions = {}) {
    this.monitor = new TrackingMonitor(options.tracking);
    this.shoot = new PressTrigger({ minMs: 100, ...options.shoot });
    this.boost = new HoldGesture({ holdMs: 250, ...options.boost });
  }

  get state(): TrackingState {
    return this.monitor.state;
  }

  /** `act: false` only follows whether the face is tracked: nothing fires and the detectors start over. */
  process(sample: GazeSample, { act = true }: { act?: boolean } = {}): HeadActionsResult {
    const present = sample.present ?? true;
    const tracking = this.monitor.update(present, sample.t);
    const intents: ControlIntent[] = tracking === "lost" ? this.endActions() : [];
    if (!act) return { intents: [...intents, ...this.endActions()], tracking };
    if (this.monitor.state !== "tracking" || !present) return { intents, tracking };

    if (sample.jaw !== undefined && this.shoot.update(sample.jaw, sample.t)) intents.push({ type: "shoot" });
    const boost = sample.brows === undefined ? null : this.boost.update(sample.brows, sample.t);
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
    this.shoot.reset();
    return this.boost.reset() === false ? [{ type: "boost", active: false }] : [];
  }
}
