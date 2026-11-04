import type { FramePerf } from "./framePerf";

type Meter = Pick<FramePerf, "component">;

/** The frame meter of the page (`?perf`), when there is one. */
function meter(): Meter | undefined {
  if (typeof window === "undefined") return undefined;
  return (window as unknown as { __snakePerf?: Meter }).__snakePerf;
}

let arrivedAtMs = 0;

/** A snapshot message has arrived: the points the page reaches afterwards are told how long after it. */
export function markArrival(atMs: number) {
  arrivedAtMs = atMs;
}

/** Runs `run` and tells the frame meter how long it took (and only it, children excluded), under `name`. */
export function timed<T>(name: string, run: () => T): T {
  const target = meter();
  if (!target) return run();
  const started = performance.now();
  try {
    return run();
  } finally {
    const now = performance.now();
    target.component(now, name, now - started);
  }
}

/** Tells the frame meter how long after the last snapshot arrived this point was reached. */
export function sinceArrival(name: string) {
  const target = meter();
  if (!target) return;
  const now = performance.now();
  target.component(now, name, now - arrivedAtMs);
}
