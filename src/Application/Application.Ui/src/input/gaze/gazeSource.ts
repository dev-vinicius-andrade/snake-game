/**
 * One reading of where the player is aiming, in stage coordinates: (0, 0) is the top-left of the game canvas
 * and (1, 1) the bottom-right. Values outside 0..1 mean outside the canvas.
 */
export interface GazeSample {
  /** Milliseconds, any origin, increasing. */
  t: number;
  x: number;
  y: number;
  /** 0..1 when the source can tell how sure it is. */
  confidence?: number;
  /** Camera sources: 0 (eyes open) to 1 (shut). Pointer sources leave it out. */
  blink?: number;
  /** Camera sources: false when no face was found in the frame. Pointer sources leave it out, meaning present. */
  present?: boolean;
  /** Head sources: how far the mouth is open, 0..1. */
  jaw?: number;
  /** Head sources: how far the eyebrows are raised, 0..1. */
  brows?: number;
  /** Camera sources: the signal before calibration, in -1..1, used to calibrate. */
  raw?: { x: number; y: number };
}

/** A device that produces aim points: the pointer (mouse, touch, hardware eye trackers acting as a mouse) or a camera. */
export interface GazeSource {
  start(): Promise<void>;
  stop(): void;
  /** Returns the function that removes the listener. */
  subscribe(listener: (sample: GazeSample) => void): () => void;
}
