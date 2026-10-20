import type { GamePoint } from "@/types/game";

export interface FramePerfOptions {
  /** How much recent history the numbers cover. */
  windowMs?: number;
}

export interface Spread {
  avg: number;
  max: number;
}

/** A frame that took over 50 ms to arrive. */
export interface Hitch {
  /** How long ago it ended. */
  agoMs: number;
  frameMs: number;
}

export interface FramePerfStats {
  fps: number;
  /** Time from one frame to the next. */
  frameMs: Spread;
  /** Time the drawing itself took. */
  drawMs: Spread;
  /** Frames the screen could have shown but did not, judged against the screen's own rate. */
  dropped: number;
  /** Frames that took over 50 ms: a stutter anyone sees. */
  longFrames: number;
  /** The longest frame since the meter started, even when the window has forgotten it. */
  worstFrameMs: number;
  /** Time between the server's snapshots. */
  snapshotMs: Spread;
  /** How uneven the snapshots arrive: the standard deviation of their interval. */
  snapshotJitterMs: number;
  /** Gaps between snapshots over 100 ms. */
  stalls: number;
  /** How long the camera's face detection takes, and how many a second run. Zero while the camera is off. */
  detectMs: Spread;
  detectHz: number;
  /** Stretches where the main thread was blocked for over 50 ms (the browser's long tasks): how many, the worst and the sum. */
  longTasks: number;
  longTaskMaxMs: number;
  longTaskTotalMs: number;
  /** Falls of the memory in use, which are garbage collections, and the least the page uses in MB a second (the rises between samples). */
  gcs: number;
  allocMBps: number;
  heapMB: number;
  /** The last frames that took over 50 ms, newest last. */
  hitches: Hitch[];
  /** Body points in the latest snapshot, and the most in the window: the size of the snapshot grows with the snakes. */
  points: number;
  maxPoints: number;
  /** How many times the world was blended and the camera stepped for each frame drawn. One is right; more is wasted work. */
  samplesPerFrame: number;
  /** Share of frames that had no newer snapshot to blend toward, so the world stood still for them. */
  starvedShare: number;
  /** How much the camera's step changes from one frame to the next, in world units. Zero for a camera that glides. */
  cameraJerk: number;
  /** Frames in which the camera turned back on itself on an axis. */
  cameraReversals: number;
}

const LONG_FRAME_MS = 50;
const STALL_MS = 100;
/** A frame this much longer than the screen's usual period counts as late. */
const LATE_FACTOR = 1.5;
/** A camera step this much bigger than the usual is a teleport (a respawn), not motion. */
const TELEPORT_STEP = 400;
/** A fall of the memory in use by at least this much is taken for a garbage collection. */
const GC_FALL_BYTES = 1024 * 1024;
const MEGABYTE = 1024 * 1024;
const MAX_HITCHES = 10;

interface Sample<T> {
  at: number;
  value: T;
}

const average = (values: number[]) => (values.length === 0 ? 0 : values.reduce((sum, value) => sum + value, 0) / values.length);
const maximum = (values: number[]) => values.reduce((most, value) => Math.max(most, value), 0);
const spread = (values: number[]): Spread => ({ avg: average(values), max: maximum(values) });

function median(values: number[]): number {
  if (values.length === 0) return 0;
  const sorted = [...values].sort((a, b) => a - b);
  return sorted[Math.floor(sorted.length / 2)];
}

function deviation(values: number[]): number {
  if (values.length < 2) return 0;
  const mean = average(values);
  return Math.sqrt(average(values.map((value) => (value - mean) ** 2)));
}

/**
 * Measures how smooth the game is: how often frames are late, how long drawing takes, how evenly the server's snapshots arrive,
 * and how the camera moves. It only collects numbers; the screen decides what to do with them.
 */
export class FramePerf {
  private readonly windowMs: number;
  private frames: Sample<{ gap: number; drawMs: number; starved: boolean }>[] = [];
  private snapshots: Sample<number>[] = [];
  private sizes: Sample<number>[] = [];
  private steps: Sample<GamePoint>[] = [];
  private samples: number[] = [];
  private tasks: Sample<number>[] = [];
  private detects: Sample<number>[] = [];
  private heaps: Sample<number>[] = [];
  private hitchList: Array<{ at: number; frameMs: number }> = [];
  private lastFrameAt: number | null = null;
  private lastSnapshotAt: number | null = null;
  private lastCamera: GamePoint | null = null;
  private worst = 0;

  constructor(options: FramePerfOptions = {}) {
    this.windowMs = options.windowMs ?? 5000;
  }

  /** One frame was drawn at `nowMs`; the drawing took `drawMs`. `starved` when there was no newer snapshot to blend toward. */
  frame(nowMs: number, drawMs: number, starved = false) {
    if (this.lastFrameAt !== null) {
      const gap = nowMs - this.lastFrameAt;
      this.worst = Math.max(this.worst, gap);
      if (gap > LONG_FRAME_MS) {
        this.hitchList.push({ at: nowMs, frameMs: gap });
        if (this.hitchList.length > MAX_HITCHES) this.hitchList.shift();
      }
      this.frames.push({ at: nowMs, value: { gap, drawMs, starved } });
    }
    this.lastFrameAt = nowMs;
    this.forget(nowMs);
  }

  /** A face detection for the camera took `durationMs`, ending at `nowMs`. */
  detect(nowMs: number, durationMs: number) {
    this.detects.push({ at: nowMs, value: durationMs });
  }

  /** The main thread was blocked for `durationMs`, until `endMs` (what the browser calls a long task). */
  longTask(endMs: number, durationMs: number) {
    this.tasks.push({ at: endMs, value: durationMs });
  }

  /** The memory in use by the page was `usedBytes` at `nowMs`. Only some browsers tell. */
  heap(nowMs: number, usedBytes: number) {
    this.heaps.push({ at: nowMs, value: usedBytes });
  }

  /** The world was blended and the camera stepped, for a frame or for something that asked where the camera is. */
  sampled(nowMs: number) {
    this.samples.push(nowMs);
  }

  /** A snapshot arrived at `nowMs`, carrying `points` body points. */
  snapshot(nowMs: number, points = 0) {
    this.sizes.push({ at: nowMs, value: points });
    if (this.lastSnapshotAt !== null) this.snapshots.push({ at: nowMs, value: nowMs - this.lastSnapshotAt });
    this.lastSnapshotAt = nowMs;
  }

  /** Where the camera was when the frame at `nowMs` was drawn. */
  camera(position: GamePoint, nowMs: number) {
    if (this.lastCamera) this.steps.push({ at: nowMs, value: { x: position.x - this.lastCamera.x, y: position.y - this.lastCamera.y } });
    this.lastCamera = position;
  }

  stats(nowMs: number): FramePerfStats {
    this.forget(nowMs);
    const gaps = this.frames.map((sample) => sample.value.gap);
    const period = median(gaps);
    const snapshotGaps = this.snapshots.map((sample) => sample.value);
    const total = gaps.reduce((sum, gap) => sum + gap, 0);
    return {
      fps: total > 0 ? (gaps.length * 1000) / total : 0,
      frameMs: spread(gaps),
      drawMs: spread(this.frames.map((sample) => sample.value.drawMs)),
      dropped: this.droppedFrames(gaps, period),
      longFrames: gaps.filter((gap) => gap > LONG_FRAME_MS).length,
      worstFrameMs: this.worst,
      snapshotMs: spread(snapshotGaps),
      snapshotJitterMs: deviation(snapshotGaps),
      stalls: snapshotGaps.filter((gap) => gap > STALL_MS).length,
      detectMs: spread(this.detects.map((sample) => sample.value)),
      detectHz: this.rate(this.detects),
      longTasks: this.tasks.length,
      longTaskMaxMs: maximum(this.tasks.map((sample) => sample.value)),
      longTaskTotalMs: this.tasks.reduce((sum, sample) => sum + sample.value, 0),
      ...this.memory(),
      hitches: this.hitchList.map((hitch) => ({ agoMs: nowMs - hitch.at, frameMs: hitch.frameMs })),
      points: this.sizes.length === 0 ? 0 : this.sizes[this.sizes.length - 1].value,
      maxPoints: maximum(this.sizes.map((sample) => sample.value)),
      samplesPerFrame: this.frames.length === 0 ? 0 : this.samples.length / this.frames.length,
      starvedShare: this.frames.length === 0 ? 0 : this.frames.filter((sample) => sample.value.starved).length / this.frames.length,
      cameraJerk: this.jerk(),
      cameraReversals: this.reversals(),
    };
  }

  /** The numbers on one line. */
  summary(nowMs: number): string {
    const stats = this.stats(nowMs);
    const one = (value: number) => value.toFixed(1);
    return (
      `${Math.round(stats.fps)} fps | frame ${one(stats.frameMs.avg)}/${one(stats.frameMs.max)} ms | draw ${one(stats.drawMs.avg)}/${one(stats.drawMs.max)} ms | ` +
      `drop ${stats.dropped} long ${stats.longFrames} | snap ${one(stats.snapshotMs.avg)} +-${one(stats.snapshotJitterMs)} ms stalls ${stats.stalls} pts ${stats.points}/${stats.maxPoints} | ` +
      `face ${one(stats.detectMs.avg)}/${one(stats.detectMs.max)} ms ${Math.round(stats.detectHz)} Hz | blocked ${stats.longTasks}x max ${one(stats.longTaskMaxMs)} ms | gc ${stats.gcs} mem ${stats.heapMB} MB +${one(stats.allocMBps)}/s | starved ${Math.round(stats.starvedShare * 100)}% | cam jerk ${one(stats.cameraJerk)} rev ${stats.cameraReversals} | work ${one(stats.samplesPerFrame)}/frame`
    );
  }

  reset() {
    this.frames = [];
    this.snapshots = [];
    this.sizes = [];
    this.steps = [];
    this.samples = [];
    this.tasks = [];
    this.detects = [];
    this.heaps = [];
    this.hitchList = [];
    this.lastFrameAt = null;
    this.lastSnapshotAt = null;
    this.lastCamera = null;
    this.worst = 0;
  }

  /** Events a second over the time they span; zero for fewer than two. */
  private rate(samples: Sample<number>[]): number {
    if (samples.length < 2) return 0;
    const span = samples[samples.length - 1].at - samples[0].at;
    return span > 0 ? ((samples.length - 1) * 1000) / span : 0;
  }

  private memory(): { gcs: number; allocMBps: number; heapMB: number } {
    if (this.heaps.length === 0) return { gcs: 0, allocMBps: 0, heapMB: 0 };
    let gcs = 0;
    let rise = 0;
    for (let index = 1; index < this.heaps.length; index++) {
      const change = this.heaps[index].value - this.heaps[index - 1].value;
      if (change <= -GC_FALL_BYTES) gcs++;
      else if (change > 0) rise += change;
    }
    const seconds = (this.heaps[this.heaps.length - 1].at - this.heaps[0].at) / 1000;
    return { gcs, allocMBps: seconds > 0 ? rise / MEGABYTE / seconds : 0, heapMB: Math.round(this.heaps[this.heaps.length - 1].value / MEGABYTE) };
  }

  /** A frame that took k periods missed k - 1 of them. */
  private droppedFrames(gaps: number[], period: number): number {
    if (period <= 0) return 0;
    return gaps.reduce((missed, gap) => (gap > period * LATE_FACTOR ? missed + Math.round(gap / period) - 1 : missed), 0);
  }

  private motion(): GamePoint[] {
    return this.steps.map((sample) => sample.value).filter((step) => Math.hypot(step.x, step.y) < TELEPORT_STEP);
  }

  /** How much the step differs from the one before, on average. A camera that glides changes its step very little. */
  private jerk(): number {
    const moves = this.motion();
    if (moves.length < 2) return 0;
    const changes = moves.slice(1).map((step, index) => Math.hypot(step.x - moves[index].x, step.y - moves[index].y));
    return average(changes);
  }

  /** A step that points against the one before it on an axis, with both big enough to see. */
  private reversals(): number {
    const moves = this.motion();
    const turned = (before: number, after: number) => Math.abs(before) > 0.5 && Math.abs(after) > 0.5 && Math.sign(before) !== Math.sign(after);
    return moves.slice(1).filter((step, index) => turned(moves[index].x, step.x) || turned(moves[index].y, step.y)).length;
  }

  private forget(nowMs: number) {
    const limit = nowMs - this.windowMs;
    this.frames = this.frames.filter((sample) => sample.at >= limit);
    this.snapshots = this.snapshots.filter((sample) => sample.at >= limit);
    this.sizes = this.sizes.filter((sample) => sample.at >= limit);
    this.steps = this.steps.filter((sample) => sample.at >= limit);
    this.samples = this.samples.filter((at) => at >= limit);
    this.tasks = this.tasks.filter((sample) => sample.at >= limit);
    this.detects = this.detects.filter((sample) => sample.at >= limit);
    this.heaps = this.heaps.filter((sample) => sample.at >= limit);
    this.hitchList = this.hitchList.filter((hitch) => hitch.at >= nowMs - this.windowMs * 6);
  }
}
