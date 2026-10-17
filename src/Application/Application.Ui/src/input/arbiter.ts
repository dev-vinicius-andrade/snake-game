import type { ControlIntent } from "./intents";

export interface InputArbiterOptions {
  /** How long a keyboard direction keeps gaze and pointer steering out of the way. */
  holdMs?: number;
}

const DEFAULT_HOLD_MS = 1000;

/**
 * Keyboard and gaze can both steer. A key press wins and holds gaze steering back for a moment, so the two
 * do not fight over the heading. Shooting and boosting never conflict with steering.
 */
export class InputArbiter {
  private readonly holdMs: number;
  private lastKeyboardDirectionAt: number | null = null;

  constructor({ holdMs = DEFAULT_HOLD_MS }: InputArbiterOptions = {}) {
    if (!(holdMs >= 0)) throw new RangeError("holdMs must not be negative");
    this.holdMs = holdMs;
  }

  noteKeyboard(intent: ControlIntent, now: number) {
    if (intent.type === "direction") this.lastKeyboardDirectionAt = now;
  }

  allowsGazeSteer(now: number): boolean {
    return this.lastKeyboardDirectionAt === null || now - this.lastKeyboardDirectionAt >= this.holdMs;
  }
}
