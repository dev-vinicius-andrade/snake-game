/** The fastest the face is looked for: about the rate of a webcam. */
export const MIN_DETECT_INTERVAL_MS = 30;
/** The slowest, so the face is still followed (10 times a second) when detection is expensive. */
export const MAX_DETECT_INTERVAL_MS = 100;
/** Detection may take about a quarter of the time: the rest is for the game. */
const TIME_PER_DETECT_COST = 4;
const SMOOTHING = 0.2;

/** How long to wait between detections, given how long one takes. Cheap detection runs at the camera's rate; costly detection less often. */
export function detectIntervalMs(averageDetectMs: number): number {
  const wanted = Number.isFinite(averageDetectMs) ? averageDetectMs * TIME_PER_DETECT_COST : 0;
  return Math.min(MAX_DETECT_INTERVAL_MS, Math.max(MIN_DETECT_INTERVAL_MS, wanted));
}

/** The running average of how long a detection takes. The first measure is taken as it is. */
export function smoothDetectTime(previous: number, measured: number): number {
  return previous <= 0 ? measured : previous + SMOOTHING * (measured - previous);
}

/** The picture asked of the camera. The head is big in the frame, so a small picture is enough; the eyes are small and need more. */
export function cameraSize(mode: "eyes" | "head"): { width: number; height: number } {
  return mode === "head" ? { width: 320, height: 240 } : { width: 640, height: 480 };
}
