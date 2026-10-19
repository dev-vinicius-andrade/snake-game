import { describe, expect, it } from "vitest";
import type { GameSnapshot, SnakeSnapshot, StrikeSnapshot } from "@/types/game";
import { FLASH_MS, LightningTracker, discPoints, lightningWarning, ringPoints } from "./lightning";

const room = { guid: null, width: 1920, height: 1080 };

const strike = (x: number, y: number, radius = 90, msLeft = 1500): StrikeSnapshot => ({ guid: `${x},${y}`, position: { x, y }, radius, msLeft });

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

const frame = (strikes: StrikeSnapshot[], snakes: SnakeSnapshot[] = []): GameSnapshot => ({ room, snakes, foods: [], shots: [], strikes });

describe("lightningWarning", () => {
  it("is null while no spot is marked", () => {
    expect(lightningWarning(frame([]), "me")).toBeNull();
  });

  it("says how long is left, for the spot that strikes first", () => {
    const warning = lightningWarning(frame([strike(100, 100, 90, 1400), strike(900, 900, 90, 600)]), "me");

    expect(warning).toEqual({ danger: false, msLeft: 600 });
  });

  it("is danger when the own head is inside a marked spot", () => {
    const warning = lightningWarning(frame([strike(500, 500)], [snake(540, 520)]), "me");

    expect(warning?.danger).toBe(true);
  });

  it("is no danger when the own head is outside every spot", () => {
    expect(lightningWarning(frame([strike(500, 500)], [snake(900, 520)]), "me")?.danger).toBe(false);
  });

  it("counts the way round the world that wraps", () => {
    const warning = lightningWarning(frame([strike(10, 500)], [snake(1900, 500)]), "me");

    expect(warning?.danger).toBe(true);
  });

  it("ignores other snakes and the dead", () => {
    const others = [snake(500, 500, { guid: "other" }), snake(500, 500, { isAlive: false })];

    expect(lightningWarning(frame([strike(500, 500)], others), "me")?.danger).toBe(false);
  });
});

describe("LightningTracker", () => {
  it("shows a ring that grows from nothing to the full radius as the time runs out", () => {
    const tracker = new LightningTracker();
    tracker.observe([strike(500, 400, 90, 1500)], 1000);

    expect(tracker.view(1000).rings[0]).toMatchObject({ center: { x: 500, y: 400 }, radius: 90, progress: 0 });
    expect(tracker.view(1750).rings[0].progress).toBeCloseTo(0.5);
    expect(tracker.view(2500).rings[0].progress).toBe(1);
    expect(tracker.view(9000).rings[0].progress).toBe(1);
  });

  it("goes on counting between snapshots and follows the server when a newer one arrives", () => {
    const tracker = new LightningTracker();
    tracker.observe([strike(500, 400, 90, 1500)], 1000);
    tracker.observe([strike(500, 400, 90, 1000)], 1500);

    expect(tracker.view(1500).rings[0].progress).toBeCloseTo(1 / 3);
    expect(tracker.view(2000).rings[0].progress).toBeCloseTo(2 / 3);
  });

  it("flashes where a warning was when the next snapshot no longer has it", () => {
    const tracker = new LightningTracker();
    tracker.observe([strike(500, 400)], 1000);
    tracker.observe([], 2500);

    expect(tracker.view(2500).rings).toEqual([]);
    expect(tracker.view(2500).flashes).toEqual([{ center: { x: 500, y: 400 }, radius: 90, age: 0 }]);
    expect(tracker.view(2500 + FLASH_MS / 2).flashes[0].age).toBeCloseTo(0.5);
    expect(tracker.view(2500 + FLASH_MS).flashes).toEqual([]);
  });

  it("keeps spots apart", () => {
    const tracker = new LightningTracker();
    tracker.observe([strike(100, 100), strike(700, 700)], 0);

    expect(tracker.view(0).rings).toHaveLength(2);
  });

  it("forgets everything on clear, without a flash", () => {
    const tracker = new LightningTracker();
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
