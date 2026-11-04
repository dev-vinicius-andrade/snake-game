import type { FramePerf, FramePerfStats, Spread } from "./framePerf";

/** What the page adds to what the frame meter measured: who it is (a random id, no one's name) and what the player was doing. */
export interface PerfContext {
  session: string;
  score: number;
  ping: number | null;
  switches: Record<string, boolean>;
  userAgent: string;
}

const DEFAULT_INTERVAL_MS = 5000;

const round = (value: number) => Math.round(value * 100) / 100;
const spread = ({ avg, max }: Spread) => ({ avg: round(avg), max: round(max) });

/** The meter's numbers as the manager takes them (`POST /telemetry/perf`): rounded, so a report is small. */
export function buildPerfReport(stats: FramePerfStats, context: PerfContext) {
  return {
    session: context.session,
    ua: context.userAgent,
    score: context.score,
    ping: context.ping,
    switches: context.switches,
    fps: round(stats.fps),
    frame: spread(stats.frameMs),
    draw: spread(stats.drawMs),
    tick: spread(stats.tickMs),
    snap: spread(stats.snapshotMs),
    snapJitter: round(stats.snapshotJitterMs),
    stalls: stats.stalls,
    points: stats.points,
    maxPoints: stats.maxPoints,
    msg: spread(stats.messageMs),
    parse: spread(stats.parseMs),
    sync: spread(stats.syncMs),
    starved: round(stats.starvedShare),
    dropped: stats.dropped,
    longFrames: stats.longFrames,
    top: stats.componentTop.map((entry) => ({
      name: entry.name,
      totalMs: round(entry.totalMs),
      count: entry.count,
      avgMs: round(entry.totalMs / entry.count),
    })),
  };
}

export interface PerfReportingOptions {
  /** The meter of the page; it does not exist until the stage is mounted. */
  perf: () => FramePerf | undefined;
  context: () => PerfContext;
  send: (report: ReturnType<typeof buildPerfReport>) => unknown;
  intervalMs?: number;
}

/** Sends a report of the meter every few seconds, until the returned function is called. A report that cannot be sent is lost, not retried. */
export function startPerfReporting(options: PerfReportingOptions): () => void {
  const timer = setInterval(() => {
    const meter = options.perf();
    if (!meter) return;
    const report = buildPerfReport(meter.stats(performance.now()), options.context());
    try {
      void Promise.resolve(options.send(report)).catch(() => undefined);
    } catch {
      // A report that cannot be sent is lost.
    }
  }, options.intervalMs ?? DEFAULT_INTERVAL_MS);
  return () => clearInterval(timer);
}
