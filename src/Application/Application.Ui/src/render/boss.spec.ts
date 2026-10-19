import { describe, expect, it } from "vitest";
import type { BossSnapshot, GameSnapshot, SnakeSnapshot } from "@/types/game";
import { armPixels, bossCompass, hpBarPixels } from "./boss";

const room = { guid: null, width: 1920, height: 1080 };

const boss = (overrides: Partial<BossSnapshot> = {}): BossSnapshot => ({
  guid: "b1",
  position: { x: 800, y: 400 },
  size: 60,
  hpRatio: 0.5,
  angle: 0,
  arms: 4,
  ...overrides,
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

const frame = (parts: Partial<GameSnapshot>): GameSnapshot => ({ room, snakes: [], foods: [], shots: [], strikes: [], boss: null, zone: null, ...parts });

describe("bossCompass", () => {
  it("is null without a boss", () => {
    expect(bossCompass(frame({ snakes: [snake(0, 0)] }), "me")).toBeNull();
  });

  it("is null without a living own snake", () => {
    expect(bossCompass(frame({ boss: boss(), snakes: [snake(0, 0, { isAlive: false })] }), "me")).toBeNull();
    expect(bossCompass(frame({ boss: boss() }), "me")).toBeNull();
  });

  it("points at the boss and says how far it is and how much is left", () => {
    const compass = bossCompass(frame({ boss: boss({ position: { x: 600, y: 300 }, hpRatio: 0.25 }), snakes: [snake(300, 300)] }), "me")!;

    expect(compass.angle).toBeCloseTo(0);
    expect(compass.distance).toBeCloseTo(300);
    expect(compass.hpRatio).toBe(0.25);
  });

  it("goes the short way round the world that wraps", () => {
    const compass = bossCompass(frame({ boss: boss({ position: { x: 20, y: 300 } }), snakes: [snake(1900, 300)] }), "me")!;

    expect(compass.angle).toBeCloseTo(0);
    expect(compass.distance).toBeCloseTo(40);
  });
});

describe("armPixels", () => {
  it("lays the barrels out evenly round the middle, pointing where the boss points", () => {
    const pixels = armPixels(boss({ arms: 4, angle: 0 }), 5);

    const right = pixels.filter((point) => point.y === 400 && point.x > 800);
    const down = pixels.filter((point) => point.x === 800 && point.y > 400);
    const left = pixels.filter((point) => point.y === 400 && point.x < 800);
    expect(right.length).toBeGreaterThan(2);
    expect(down.length).toBeGreaterThan(2);
    expect(left.length).toBeGreaterThan(2);
  });

  it("turns with the boss", () => {
    const straight = armPixels(boss({ arms: 1, angle: 0 }), 5);
    const turned = armPixels(boss({ arms: 1, angle: Math.PI / 2 }), 5);

    expect(Math.max(...straight.map((point) => point.x))).toBeGreaterThan(Math.max(...turned.map((point) => point.x)));
    expect(Math.max(...turned.map((point) => point.y))).toBeGreaterThan(Math.max(...straight.map((point) => point.y)));
  });

  it("reaches out past the body of the boss, and has no pixel twice, all on the pixel grid", () => {
    const pixels = armPixels(boss({ arms: 6, angle: 0.3 }), 5);

    expect(Math.max(...pixels.map((point) => Math.hypot(point.x - 800, point.y - 400)))).toBeGreaterThan(30);
    expect(new Set(pixels.map((point) => `${point.x},${point.y}`)).size).toBe(pixels.length);
    expect(pixels.every((point) => point.x % 5 === 0 && point.y % 5 === 0)).toBe(true);
  });
});

describe("hpBarPixels", () => {
  it("is as wide as the boss, and fills as much as is left", () => {
    const bar = hpBarPixels(boss({ size: 60, hpRatio: 0.5 }), 5);

    expect(bar.width).toBe(12);
    expect(bar.filled).toBe(6);
  });

  it("always shows something while the boss stands, and everything at full health", () => {
    expect(hpBarPixels(boss({ hpRatio: 0.01 }), 5).filled).toBe(1);
    expect(hpBarPixels(boss({ hpRatio: 1 }), 5).filled).toBe(12);
  });

  it("sits above the boss, centred on it", () => {
    const bar = hpBarPixels(boss({ position: { x: 800, y: 400 }, size: 60 }), 5);

    expect(bar.origin.x).toBe(770);
    expect(bar.origin.y).toBeLessThan(370);
    expect(bar.origin.x % 5).toBe(0);
    expect(bar.origin.y % 5).toBe(0);
  });
});
