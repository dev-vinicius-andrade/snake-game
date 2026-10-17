import { pointToStage, type StageRect } from "@/render/stageGeometry";
import type { GazeSample, GazeSource } from "./gazeSource";

type Listener = (event: any) => void;

/** What the source needs from `window` (or any element), so specs can drive it with a fake. */
export interface PointerEventTarget {
  addEventListener(type: string, listener: Listener): void;
  removeEventListener(type: string, listener: Listener): void;
}

export interface PointerSourceOptions {
  target: PointerEventTarget;
  /** Current position of the canvas; null while it has no size. */
  getRect: () => StageRect | null;
}

interface PointerLikeEvent {
  clientX: number;
  clientY: number;
  timeStamp: number;
}

/**
 * Aim with the pointer. Mice and touch work as is, and so do eye trackers that move the system cursor
 * (Tobii and similar), which makes this the hardware route to gaze control.
 */
export function createPointerSource({ target, getRect }: PointerSourceOptions): GazeSource {
  const listeners = new Set<(sample: GazeSample) => void>();
  let running = false;

  const onMove = (event: PointerLikeEvent) => {
    const rect = getRect();
    const point = rect ? pointToStage(event.clientX, event.clientY, rect) : null;
    if (!point) return;
    const sample: GazeSample = { t: event.timeStamp, x: point.x, y: point.y };
    listeners.forEach((listener) => listener(sample));
  };

  return {
    async start() {
      if (running) return;
      running = true;
      target.addEventListener("pointermove", onMove);
    },
    stop() {
      if (!running) return;
      running = false;
      target.removeEventListener("pointermove", onMove);
    },
    subscribe(listener) {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
  };
}
