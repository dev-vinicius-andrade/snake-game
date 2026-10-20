import { describe, expect, it } from "vitest";
import { FramePerf } from "./framePerf";

/** Feeds `count` frames `gapMs` apart, each taking `drawMs` to draw, starting at `start`. Returns the time after the last one. */
function frames(perf: FramePerf, count: number, gapMs: number, drawMs = 2, start = 1000): number {
  let now = start;
  for (let index = 0; index < count; index++) {
    perf.frame(now, drawMs);
    now += gapMs;
  }
  return now;
}

describe("FramePerf frames", () => {
  it("reports the frame rate and the frame time of a steady 60 Hz", () => {
    const perf = new FramePerf();
    const end = frames(perf, 120, 1000 / 60);

    const stats = perf.stats(end);

    expect(stats.fps).toBeCloseTo(60, 0);
    expect(stats.frameMs.avg).toBeCloseTo(16.67, 1);
    expect(stats.frameMs.max).toBeCloseTo(16.67, 1);
    expect(stats.dropped).toBe(0);
    expect(stats.longFrames).toBe(0);
  });

  it("counts a frame that took three refresh periods as two dropped frames", () => {
    const perf = new FramePerf();
    let now = frames(perf, 60, 1000 / 60);
    perf.frame(now, 2);
    now += 50;
    perf.frame(now, 2);
    now = frames(perf, 30, 1000 / 60, 2, now + 1000 / 60);

    const stats = perf.stats(now);

    expect(stats.dropped).toBe(2);
    expect(stats.frameMs.max).toBeCloseTo(50, 0);
  });

  it("calls a frame over 50 ms a long frame", () => {
    const perf = new FramePerf();
    let now = frames(perf, 60, 1000 / 60);
    perf.frame(now, 2);
    now += 120;
    perf.frame(now, 2);

    expect(perf.stats(now).longFrames).toBe(1);
  });

  it("judges drops against the screen's own rate, not a fixed 60 Hz", () => {
    const perf = new FramePerf();
    const end = frames(perf, 200, 1000 / 144);

    expect(perf.stats(end).dropped).toBe(0);
    expect(perf.stats(end).fps).toBeCloseTo(144, 0);
  });

  it("measures how long drawing takes", () => {
    const perf = new FramePerf();
    perf.frame(983, 1);
    let now = 1000;
    for (const drawMs of [2, 4, 12]) {
      perf.frame(now, drawMs);
      now += 17;
    }

    const stats = perf.stats(now);

    expect(stats.drawMs.avg).toBeCloseTo(6, 5);
    expect(stats.drawMs.max).toBe(12);
  });

  it("forgets what is older than the window", () => {
    const perf = new FramePerf({ windowMs: 2000 });
    perf.frame(1000, 2);
    perf.frame(1017, 400);
    const end = frames(perf, 150, 17, 1, 5000);

    expect(perf.stats(end).drawMs.max).toBe(1);
  });

  it("says nothing before there are frames", () => {
    const stats = new FramePerf().stats(1000);

    expect(stats.fps).toBe(0);
    expect(stats.frameMs.avg).toBe(0);
    expect(stats.samplesPerFrame).toBe(0);
  });

  it("keeps the worst frame since the start, for a short stall the window already forgot", () => {
    const perf = new FramePerf({ windowMs: 1000 });
    perf.frame(0, 1);
    perf.frame(300, 1);
    const end = frames(perf, 200, 17, 1, 317);

    expect(perf.stats(end).worstFrameMs).toBeCloseTo(300, 0);
  });
});

describe("FramePerf snapshots", () => {
  it("reports the interval between snapshots and its jitter", () => {
    const perf = new FramePerf();
    const gaps = [33, 34, 33, 60, 33, 34, 33, 33];
    let at = 1000;
    perf.snapshot(at);
    for (const gap of gaps) {
      at += gap;
      perf.snapshot(at);
    }

    const stats = perf.stats(at);

    expect(stats.snapshotMs.avg).toBeCloseTo(36.6, 0);
    expect(stats.snapshotMs.max).toBe(60);
    expect(stats.snapshotJitterMs).toBeGreaterThan(5);
  });

  it("counts gaps over 100 ms as stalls", () => {
    const perf = new FramePerf();
    perf.snapshot(1000);
    perf.snapshot(1033);
    perf.snapshot(1200);

    expect(perf.stats(1200).stalls).toBe(1);
  });

  it("counts how often the buffer had nothing newer to show", () => {
    const perf = new FramePerf();
    perf.frame(983, 1);
    let now = 1000;
    for (let index = 0; index < 100; index++) {
      perf.frame(now, 1, index % 4 === 0);
      now += 16.7;
    }

    const stats = perf.stats(now);

    expect(stats.starvedShare).toBeCloseTo(0.25, 2);
  });
});

describe("FramePerf stalls of the page", () => {
  it("counts long tasks: stretches where the browser's main thread was blocked", () => {
    const perf = new FramePerf();
    perf.longTask(2000, 330);
    perf.longTask(3000, 60);

    const stats = perf.stats(3100);

    expect(stats.longTasks).toBe(2);
    expect(stats.longTaskMaxMs).toBe(330);
    expect(stats.longTaskTotalMs).toBe(390);
  });

  it("forgets long tasks older than the window", () => {
    const perf = new FramePerf({ windowMs: 2000 });
    perf.longTask(1000, 300);

    expect(perf.stats(9000).longTasks).toBe(0);
  });

  it("reads a fall in the memory in use as a garbage collection, and the rises as the least the page uses a second", () => {
    const perf = new FramePerf();
    const MB = 1024 * 1024;
    perf.heap(0, 50 * MB);
    perf.heap(250, 54 * MB);
    perf.heap(500, 58 * MB);
    perf.heap(750, 40 * MB);
    perf.heap(1000, 44 * MB);

    const stats = perf.stats(1000);

    expect(stats.gcs).toBe(1);
    expect(stats.allocMBps).toBeCloseTo(12, 0);
    expect(stats.heapMB).toBe(44);
  });

  it("does not take a small fall for a collection", () => {
    const perf = new FramePerf();
    perf.heap(0, 50 * 1024 * 1024);
    perf.heap(250, 50 * 1024 * 1024 - 200_000);

    expect(perf.stats(250).gcs).toBe(0);
  });

  it("says nothing about memory when the browser does not tell", () => {
    const stats = new FramePerf().stats(1000);

    expect(stats.gcs).toBe(0);
    expect(stats.heapMB).toBe(0);
  });

  it("lists the recent hitches, newest last, with how long ago and how long", () => {
    const perf = new FramePerf();
    perf.frame(0, 1);
    perf.frame(16, 1);
    perf.frame(500, 1);
    perf.frame(517, 1);
    perf.frame(900, 1);

    const hitches = perf.stats(1000).hitches;

    expect(hitches.map((hitch) => Math.round(hitch.frameMs))).toEqual([484, 383]);
    expect(hitches[1].agoMs).toBeCloseTo(100, 0);
  });
});

describe("FramePerf snapshot size", () => {
  it("reports how many body points the latest snapshot carried and the biggest in the window", () => {
    const perf = new FramePerf();
    perf.snapshot(1000, 40);
    perf.snapshot(1033, 900);
    perf.snapshot(1066, 120);

    const stats = perf.stats(1066);

    expect(stats.points).toBe(120);
    expect(stats.maxPoints).toBe(900);
  });

  it("is zero when nothing said how big the snapshots were", () => {
    const perf = new FramePerf();
    perf.snapshot(1000);
    perf.snapshot(1033);

    expect(perf.stats(1033).points).toBe(0);
  });
});

describe("FramePerf face detection", () => {
  it("reports how long the camera's face detection takes and how often it runs", () => {
    const perf = new FramePerf();
    for (let index = 0; index < 20; index++) perf.detect(1000 + index * 50, index === 10 ? 30 : 6);

    const stats = perf.stats(2000);

    expect(stats.detectMs.avg).toBeCloseTo(7.2, 1);
    expect(stats.detectMs.max).toBe(30);
    expect(stats.detectHz).toBeCloseTo(20, 0);
  });

  it("says nothing when the camera is off", () => {
    const stats = new FramePerf().stats(1000);

    expect(stats.detectMs).toEqual({ avg: 0, max: 0 });
    expect(stats.detectHz).toBe(0);
  });

  it("forgets old detections", () => {
    const perf = new FramePerf({ windowMs: 2000 });
    perf.detect(1000, 50);

    expect(perf.stats(9000).detectMs.max).toBe(0);
  });
});

describe("FramePerf work", () => {
  it("counts how many times the world was worked out for each frame drawn", () => {
    const perf = new FramePerf();
    perf.frame(983, 1);
    let now = 1000;
    for (let index = 0; index < 50; index++) {
      for (let call = 0; call < 3; call++) perf.sampled(now);
      perf.frame(now, 1);
      now += 16.7;
    }

    expect(perf.stats(now).samplesPerFrame).toBeCloseTo(3, 0);
  });
});

describe("FramePerf camera", () => {
  it("is calm when the camera moves in a straight line at a steady speed", () => {
    const perf = new FramePerf();
    let now = 1000;
    for (let index = 0; index < 120; index++) {
      perf.camera({ x: index * 3, y: 100 }, now);
      perf.frame(now, 1);
      now += 16.7;
    }

    const stats = perf.stats(now);

    expect(stats.cameraJerk).toBeLessThan(0.05);
    expect(stats.cameraReversals).toBe(0);
  });

  it("shows a camera that keeps stopping and jumping as jerk", () => {
    const perf = new FramePerf();
    let now = 1000;
    let x = 0;
    for (let index = 0; index < 120; index++) {
      x += index % 2 === 0 ? 0 : 6;
      perf.camera({ x, y: 100 }, now);
      perf.frame(now, 1);
      now += 16.7;
    }

    expect(perf.stats(now).cameraJerk).toBeGreaterThan(2);
  });

  it("counts a camera that moves back and forth as reversals", () => {
    const perf = new FramePerf();
    let now = 1000;
    for (let index = 0; index < 60; index++) {
      perf.camera({ x: index % 2 === 0 ? 0 : 4, y: 100 }, now);
      perf.frame(now, 1);
      now += 16.7;
    }

    expect(perf.stats(now).cameraReversals).toBeGreaterThan(40);
  });

  it("ignores a teleport (respawn) so it does not count as jerk", () => {
    const perf = new FramePerf();
    let now = 1000;
    for (let index = 0; index < 60; index++) {
      perf.camera({ x: index < 30 ? index * 3 : 5000 + index * 3, y: 100 }, now);
      perf.frame(now, 1);
      now += 16.7;
    }

    expect(perf.stats(now).cameraJerk).toBeLessThan(0.5);
  });
});

describe("FramePerf summary", () => {
  it("gives one line for people to read", () => {
    const perf = new FramePerf();
    const end = frames(perf, 120, 1000 / 60);

    expect(perf.summary(end)).toMatch(/^60 fps .*drop 0/);
  });
});
