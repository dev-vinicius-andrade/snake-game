import { describe, expect, it } from "vitest";
import type { GameSnapshot, SnakeSnapshot, StrikeKind, StrikeSnapshot } from "@/types/game";
import { FLASH_MS, StrikeTracker, discPoints, discRows, ringPoints, strikeWarning } from "./strikes";

const room = { guid: null, width: 1920, height: 1080 };

const strike = (x: number, y: number, radius = 90, msLeft = 1500, kind: StrikeKind = "lightning"): StrikeSnapshot => ({
  guid: `${x},${y}`,
  kind,
  position: { x, y },
  radius,
  msLeft,
});

function snake(headX: number, headY: number, overrides: Partial<SnakeSnapshot> = {}): SnakeSnapshot {
  const head = { x: headX, y: headY };
  return {
    guid: "me",
    connectionId: "c",
    name: "ana",
    head,
    path: [head],
    segmentHealth: [1],
    isAlive: true,
    isBoosting: false,
    perks: [],
    autoFire: false,
    foodsEaten: 0,
    score: 0,
    color: { fillColor: "#00ff00", border: "#00ff00" },
    direction: "right",
    ...overrides,
  };
}

const frame = (strikes: StrikeSnapshot[], snakes: SnakeSnapshot[] = []): GameSnapshot => ({ room, snakes, foods: [], shots: [], strikes, boss: null });

describe("strikeWarning", () => {
  it("is null while no spot is marked", () => {
    expect(strikeWarning(frame([]), "me")).toBeNull();
  });

  it("says how long is left, for the spot that strikes first", () => {
    const warning = strikeWarning(frame([strike(100, 100, 90, 1400), strike(900, 900, 90, 600)]), "me");

    expect(warning).toEqual({ kind: "lightning", danger: false, msLeft: 600 });
  });

  it("is danger when the own head is inside a marked spot", () => {
    const warning = strikeWarning(frame([strike(500, 500)], [snake(540, 520)]), "me");

    expect(warning?.danger).toBe(true);
  });

  it("is no danger when the own head is outside every spot", () => {
    expect(strikeWarning(frame([strike(500, 500)], [snake(900, 520)]), "me")?.danger).toBe(false);
  });

  it("counts the way round the world that wraps", () => {
    const warning = strikeWarning(frame([strike(10, 500)], [snake(1900, 500)]), "me");

    expect(warning?.danger).toBe(true);
  });

  it("names a meteor when the soonest strike is one", () => {
    const warning = strikeWarning(frame([strike(100, 100, 130, 1800, "meteor"), strike(900, 900, 90, 2500)]), "me");

    expect(warning?.kind).toBe("meteor");
  });

  it("names what the own snake is in the way of, even when something else strikes sooner", () => {
    const warning = strikeWarning(frame([strike(900, 900, 90, 200), strike(500, 500, 130, 1900, "meteor")], [snake(520, 520)]), "me");

    expect(warning).toEqual({ kind: "meteor", danger: true, msLeft: 200 });
  });

  it("ignores other snakes and the dead", () => {
    const others = [snake(500, 500, { guid: "other" }), snake(500, 500, { isAlive: false })];

    expect(strikeWarning(frame([strike(500, 500)], others), "me")?.danger).toBe(false);
  });
});

describe("StrikeTracker", () => {
  it("shows a ring that grows from nothing to the full radius as the time runs out", () => {
    const tracker = new StrikeTracker();
    tracker.observe([strike(500, 400, 90, 1500)], 1000);

    expect(tracker.view(1000).rings[0]).toMatchObject({ center: { x: 500, y: 400 }, radius: 90, progress: 0, kind: "lightning" });
    expect(tracker.view(1750).rings[0].progress).toBeCloseTo(0.5);
    expect(tracker.view(2500).rings[0].progress).toBe(1);
    expect(tracker.view(9000).rings[0].progress).toBe(1);
  });

  it("goes on counting between snapshots and follows the server when a newer one arrives", () => {
    const tracker = new StrikeTracker();
    tracker.observe([strike(500, 400, 90, 1500)], 1000);
    tracker.observe([strike(500, 400, 90, 1000)], 1500);

    expect(tracker.view(1500).rings[0].progress).toBeCloseTo(1 / 3);
    expect(tracker.view(2000).rings[0].progress).toBeCloseTo(2 / 3);
  });

  it("flashes where a warning was when the next snapshot no longer has it", () => {
    const tracker = new StrikeTracker();
    tracker.observe([strike(500, 400)], 1000);
    tracker.observe([], 2500);

    expect(tracker.view(2500).rings).toEqual([]);
    expect(tracker.view(2500).flashes).toEqual([{ center: { x: 500, y: 400 }, radius: 90, age: 0, kind: "lightning" }]);
    expect(tracker.view(2500 + FLASH_MS / 2).flashes[0].age).toBeCloseTo(0.5);
    expect(tracker.view(2500 + FLASH_MS).flashes).toEqual([]);
  });

  it("keeps the kind of a meteor on its ring and its flash", () => {
    const tracker = new StrikeTracker();
    tracker.observe([strike(500, 400, 130, 2000, "meteor")], 0);
    expect(tracker.view(0).rings[0].kind).toBe("meteor");

    tracker.observe([], 2000);

    expect(tracker.view(2000).flashes[0].kind).toBe("meteor");
  });

  it("counts a meteor over its own, longer warning", () => {
    const tracker = new StrikeTracker();
    tracker.observe([strike(500, 400, 130, 2000, "meteor")], 0);

    expect(tracker.view(1000).rings[0].progress).toBeCloseTo(0.5);
  });

  it("keeps a storm of many spots apart, and flashes each as it falls", () => {
    const tracker = new StrikeTracker();
    tracker.observe([strike(100, 100, 90, 1500), strike(700, 700, 90, 1500), strike(900, 300, 90, 1500)], 0);
    tracker.observe([strike(700, 700, 90, 600), strike(900, 300, 90, 600)], 900);

    expect(tracker.view(900).rings).toHaveLength(2);
    expect(tracker.view(900).flashes.map((flash) => flash.center)).toEqual([{ x: 100, y: 100 }]);
  });

  it("keeps spots apart", () => {
    const tracker = new StrikeTracker();
    tracker.observe([strike(100, 100), strike(700, 700)], 0);

    expect(tracker.view(0).rings).toHaveLength(2);
  });

  it("forgets everything on clear, without a flash", () => {
    const tracker = new StrikeTracker();
    tracker.observe([strike(100, 100)], 0);
    tracker.clear();
    tracker.observe([], 100);

    expect(tracker.view(100)).toEqual({ rings: [], flashes: [] });
  });
});

describe("ringPoints", () => {
  const center = { x: 500, y: 400 };
  const points = ringPoints(center, 90, 5);

  it("lies on the circle, on the pixel grid", () => {
    expect(points.length).toBeGreaterThan(40);
    for (const point of points) {
      expect(Math.abs(Math.hypot(point.x - center.x, point.y - center.y) - 90)).toBeLessThanOrEqual(5 * Math.SQRT2);
      expect(point.x % 5).toBe(0);
      expect(point.y % 5).toBe(0);
    }
  });

  it("has no point twice", () => {
    expect(new Set(points.map((point) => `${point.x},${point.y}`)).size).toBe(points.length);
  });

  it("is a single pixel for a radius too small to see", () => {
    expect(ringPoints(center, 0, 5)).toHaveLength(1);
  });
});

describe("discPoints", () => {
  it("fills the circle on the pixel grid, about pi r squared", () => {
    const points = discPoints({ x: 500, y: 400 }, 90, 5);

    expect(points.length).toBeGreaterThan(Math.PI * 18 * 18 * 0.9);
    expect(points.length).toBeLessThan(Math.PI * 18 * 18 * 1.1);
    expect(points.every((point) => point.x % 5 === 0 && point.y % 5 === 0)).toBe(true);
  });
});

describe("discRows", () => {
  const rows = discRows({ x: 500, y: 400 }, 130, 5);

  it("covers about pi r squared, one row of pixels at a time, on the pixel grid", () => {
    const area = rows.reduce((sum, row) => sum + row.width, 0);

    expect(area).toBeGreaterThan(Math.PI * 26 * 26 * 0.9);
    expect(area).toBeLessThan(Math.PI * 26 * 26 * 1.1);
    expect(rows.every((row) => row.x % 5 === 0 && row.y % 5 === 0 && row.width >= 1)).toBe(true);
  });

  it("is widest in the middle and narrow at the top", () => {
    const middle = rows.find((row) => row.y === 400)!;

    expect(middle.width).toBeGreaterThan(rows[0].width);
  });

  it("has one row for every row of pixels", () => {
    expect(new Set(rows.map((row) => row.y)).size).toBe(rows.length);
  });
});
