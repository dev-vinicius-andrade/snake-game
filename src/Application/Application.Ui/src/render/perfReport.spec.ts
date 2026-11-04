import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { FramePerf } from "./framePerf";
import { buildPerfReport, startPerfReporting, type PerfContext } from "./perfReport";

const context: PerfContext = {
  session: "abc",
  score: 46,
  ping: 3782,
  switches: { perks: true, touch: false, hud: true, hints: true },
  userAgent: "iPhone",
};

describe("buildPerfReport", () => {
  it("names the page and what it was doing", () => {
    const report = buildPerfReport(new FramePerf().stats(1000), context);

    expect(report).toMatchObject({ session: "abc", score: 46, ping: 3782, ua: "iPhone", switches: { touch: false } });
  });

  it("carries where a snapshot's time goes, with the average of each part", () => {
    const perf = new FramePerf();
    perf.message(1000, 40);
    perf.messageParts(1000, 1, 2);
    perf.component(1000, "stage buffer", 6);
    perf.component(1010, "stage buffer", 4);

    const report = buildPerfReport(perf.stats(1100), context);

    expect(report).toMatchObject({ msg: { avg: 40, max: 40 }, parse: { avg: 1, max: 1 }, sync: { avg: 2, max: 2 } });
    expect(report.top).toEqual([{ name: "stage buffer", totalMs: 10, count: 2, avgMs: 5 }]);
  });

  it("rounds the numbers so the report stays small", () => {
    const perf = new FramePerf();
    perf.message(1000, 1 / 3);

    expect(buildPerfReport(perf.stats(1100), context).msg).toEqual({ avg: 0.33, max: 0.33 });
  });
});

describe("startPerfReporting", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it("sends a report every interval, until it is stopped", () => {
    const send = vi.fn();
    const stop = startPerfReporting({ perf: () => new FramePerf(), context: () => context, send, intervalMs: 5000 });

    vi.advanceTimersByTime(10000);
    stop();
    vi.advanceTimersByTime(10000);

    expect(send).toHaveBeenCalledTimes(2);
    expect(send.mock.calls[0][0]).toMatchObject({ session: "abc" });
  });

  it("waits for the meter when the page has none yet", () => {
    const send = vi.fn();
    startPerfReporting({ perf: () => undefined, context: () => context, send, intervalMs: 1000 });

    vi.advanceTimersByTime(3000);

    expect(send).not.toHaveBeenCalled();
  });

  it("goes on when a send fails", async () => {
    const send = vi.fn().mockRejectedValue(new Error("offline"));
    startPerfReporting({ perf: () => new FramePerf(), context: () => context, send, intervalMs: 1000 });

    await vi.advanceTimersByTimeAsync(3000);

    expect(send).toHaveBeenCalledTimes(3);
  });
});
