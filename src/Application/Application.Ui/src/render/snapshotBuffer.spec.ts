import { describe, expect, it } from "vitest";
import type { GameSnapshot, ShotSnapshot, SnakeSnapshot } from "@/types/game";
import { SnapshotBuffer, blendSnapshots } from "./snapshotBuffer";

const room = { guid: null, width: 1000, height: 1000 };

function snake(path: Array<[number, number]>, overrides: Partial<SnakeSnapshot> = {}): SnakeSnapshot {
  const points = path.map(([x, y]) => ({ x, y }));
  return {
    guid: "s1",
    connectionId: "c1",
    name: "ana",
    head: points[points.length - 1],
    path: points,
    segmentHealth: points.map(() => 1),
    isAlive: true,
    isBoosting: false,
    score: 0,
    color: { fillColor: "#00ff00", border: "#00ff00" },
    direction: "right",
    ...overrides,
  };
}

const shot = (guid: string, x: number, y: number): ShotSnapshot => ({ guid, position: { x, y }, size: 15 });
const frame = (parts: Partial<GameSnapshot> = {}): GameSnapshot => ({ room, snakes: [], foods: [], shots: [], ...parts });

describe("blendSnapshots", () => {
  it("is the first snapshot at 0 and the second at 1", () => {
    const from = frame({ snakes: [snake([[0, 0], [10, 0]])] });
    const to = frame({ snakes: [snake([[30, 0], [40, 0]])] });

    expect(blendSnapshots(from, to, 0).snakes[0].head.x).toBe(10);
    expect(blendSnapshots(from, to, 1).snakes[0].head.x).toBe(40);
  });

  it("moves every point of a snake part of the way", () => {
    const from = frame({ snakes: [snake([[0, 0], [10, 0]])] });
    const to = frame({ snakes: [snake([[30, 0], [40, 0]])] });

    const middle = blendSnapshots(from, to, 0.5).snakes[0];

    expect(middle.path).toEqual([{ x: 15, y: 0 }, { x: 25, y: 0 }]);
    expect(middle.head).toEqual({ x: 25, y: 0 });
  });

  it("keeps head as the last point of the path", () => {
    const from = frame({ snakes: [snake([[0, 0], [10, 5]])] });
    const to = frame({ snakes: [snake([[30, 0], [40, 9]])] });

    const middle = blendSnapshots(from, to, 0.3).snakes[0];

    expect(middle.head).toBe(middle.path[middle.path.length - 1]);
  });

  it("goes the short way round the world when a snake crosses an edge", () => {
    const from = frame({ snakes: [snake([[990, 500]])] });
    const to = frame({ snakes: [snake([[10, 500]])] });

    expect(blendSnapshots(from, to, 0.5).snakes[0].head.x).toBe(0);
    expect(blendSnapshots(from, to, 0.25).snakes[0].head.x).toBe(995);
  });

  it("stays inside the world", () => {
    const from = frame({ snakes: [snake([[995, 995]])] });
    const to = frame({ snakes: [snake([[5, 5]])] });

    for (let t = 0; t <= 1; t += 0.1) {
      const { head } = blendSnapshots(from, to, t).snakes[0];
      expect(head.x).toBeGreaterThanOrEqual(0);
      expect(head.x).toBeLessThan(1000);
      expect(head.y).toBeGreaterThanOrEqual(0);
      expect(head.y).toBeLessThan(1000);
    }
  });

  it("aligns the points from the head when the snake grew, and leaves the new tail point alone", () => {
    const from = frame({ snakes: [snake([[10, 0], [20, 0]])] });
    const to = frame({ snakes: [snake([[10, 0], [30, 0], [40, 0]])] });

    const middle = blendSnapshots(from, to, 0.5).snakes[0];

    expect(middle.path).toEqual([{ x: 10, y: 0 }, { x: 20, y: 0 }, { x: 30, y: 0 }]);
  });

  it("aligns from the head when the snake shrank", () => {
    const from = frame({ snakes: [snake([[0, 0], [10, 0], [20, 0]])] });
    const to = frame({ snakes: [snake([[30, 0], [40, 0]])] });

    expect(blendSnapshots(from, to, 0.5).snakes[0].path).toEqual([{ x: 20, y: 0 }, { x: 30, y: 0 }]);
  });

  it("takes everything else from the newer snapshot", () => {
    const from = frame({ snakes: [snake([[0, 0]], { score: 1, segmentHealth: [1], isBoosting: false })] });
    const to = frame({ snakes: [snake([[10, 0]], { score: 5, segmentHealth: [0.4], isBoosting: true })] });

    const middle = blendSnapshots(from, to, 0.5).snakes[0];

    expect(middle).toMatchObject({ score: 5, segmentHealth: [0.4], isBoosting: true });
  });

  it("shows a snake that only the newer snapshot has, as it is", () => {
    const from = frame({ snakes: [] });
    const to = frame({ snakes: [snake([[10, 0]])] });

    expect(blendSnapshots(from, to, 0.5).snakes).toHaveLength(1);
  });

  it("does not show a snake that is gone in the newer snapshot", () => {
    const from = frame({ snakes: [snake([[10, 0]])] });
    const to = frame({ snakes: [] });

    expect(blendSnapshots(from, to, 0.5).snakes).toHaveLength(0);
  });

  it("moves shots by their id", () => {
    const from = frame({ shots: [shot("a", 0, 0), shot("b", 100, 100)] });
    const to = frame({ shots: [shot("b", 120, 100), shot("a", 40, 0)] });

    const shots = blendSnapshots(from, to, 0.5).shots;

    expect(shots.find((s) => s.guid === "a")!.position).toEqual({ x: 20, y: 0 });
    expect(shots.find((s) => s.guid === "b")!.position).toEqual({ x: 110, y: 100 });
  });

  it("takes food, which does not move, from the newer snapshot", () => {
    const food = { guid: "f", position: { x: 5, y: 5 }, color: { fillColor: "#fff", border: "#fff" }, value: 1 };

    expect(blendSnapshots(frame(), frame({ foods: [food] }), 0.5).foods).toEqual([food]);
  });

  it("does not change the snapshots it is given", () => {
    const from = frame({ snakes: [snake([[0, 0], [10, 0]])] });
    const to = frame({ snakes: [snake([[30, 0], [40, 0]])] });
    const before = JSON.stringify([from, to]);

    blendSnapshots(from, to, 0.5);

    expect(JSON.stringify([from, to])).toBe(before);
  });
});

describe("SnapshotBuffer", () => {
  const at = (x: number) => frame({ snakes: [snake([[x, 0]])] });
  const headAt = (buffer: SnapshotBuffer, now: number) => buffer.sample(now)?.snakes[0].head.x;

  it("has nothing to show before the first snapshot", () => {
    expect(new SnapshotBuffer().sample(100)).toBeNull();
  });

  it("shows the only snapshot it has", () => {
    const buffer = new SnapshotBuffer({ delayMs: 50 });
    buffer.push(at(100), 1000);

    expect(headAt(buffer, 1000)).toBe(100);
    expect(headAt(buffer, 5000)).toBe(100);
  });

  it("shows the world a little in the past, so there is always a snapshot ahead to blend toward", () => {
    const buffer = new SnapshotBuffer({ delayMs: 50 });
    buffer.push(at(100), 1000);
    buffer.push(at(130), 1033);

    // 1033 - 50 = 983 is before the first snapshot: it still shows the first.
    expect(headAt(buffer, 1033)).toBe(100);
  });

  it("blends between two snapshots according to when it is", () => {
    const buffer = new SnapshotBuffer({ delayMs: 50 });
    buffer.push(at(100), 1000);
    buffer.push(at(130), 1033);
    buffer.push(at(160), 1066);

    // 1066 - 50 = 1016 is about half way between 1000 and 1033.
    const x = headAt(buffer, 1066)!;
    expect(x).toBeGreaterThan(100);
    expect(x).toBeLessThan(130);
    expect(x).toBeCloseTo(100 + (30 * 16) / 33, 5);
  });

  it("moves forward smoothly as time passes between snapshots", () => {
    const buffer = new SnapshotBuffer({ delayMs: 50 });
    buffer.push(at(0), 1000);
    buffer.push(at(30), 1033);
    buffer.push(at(60), 1066);

    const xs = [1050, 1055, 1060, 1065, 1070, 1075, 1080].map((now) => headAt(buffer, now)!);
    for (let i = 1; i < xs.length; i++) expect(xs[i]).toBeGreaterThan(xs[i - 1]);
  });

  it("shows the newest snapshot, without guessing ahead, when time has run past it", () => {
    const buffer = new SnapshotBuffer({ delayMs: 50 });
    buffer.push(at(100), 1000);
    buffer.push(at(130), 1033);

    expect(headAt(buffer, 2000)).toBe(130);
  });

  it("ignores a snapshot that arrives with an earlier time than the last one", () => {
    const buffer = new SnapshotBuffer({ delayMs: 0 });
    buffer.push(at(100), 1000);
    buffer.push(at(130), 1033);
    buffer.push(at(999), 1020);

    expect(headAt(buffer, 1033)).toBe(130);
  });

  it("keeps only recent snapshots", () => {
    const buffer = new SnapshotBuffer({ delayMs: 0, maxAgeMs: 200 });
    for (let t = 0; t <= 2000; t += 33) buffer.push(at(t), t);

    expect(buffer.size).toBeLessThan(15);
  });

  it("can be emptied", () => {
    const buffer = new SnapshotBuffer();
    buffer.push(at(1), 1000);

    buffer.clear();

    expect(buffer.sample(1000)).toBeNull();
  });

  it("copes with two snapshots at the same time", () => {
    const buffer = new SnapshotBuffer({ delayMs: 0 });
    buffer.push(at(100), 1000);
    buffer.push(at(130), 1000);

    expect(headAt(buffer, 1000)).toBe(130);
  });
});
