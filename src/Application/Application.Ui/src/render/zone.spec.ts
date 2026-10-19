import { describe, expect, it } from "vitest";
import type { GameSnapshot, SnakeSnapshot, ZoneSnapshot } from "@/types/game";
import { isOutsideZone, zoneWarning } from "./zone";

const room = { guid: null, width: 1920, height: 1080 };

const zone = (overrides: Partial<ZoneSnapshot> = {}): ZoneSnapshot => ({
  position: { x: 960, y: 540 },
  radius: 300,
  finalRadius: 160,
  phase: "closing",
  msLeft: 12000,
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
    perkTiers: [],
    autoFire: false,
    foodsEaten: 0,
    score: 0,
    color: { fillColor: "#00ff00", border: "#00ff00" },
    direction: "right",
    ...overrides,
  };
}

const frame = (parts: Partial<GameSnapshot>): GameSnapshot => ({ room, snakes: [], foods: [], shots: [], strikes: [], boss: null, zone: null, ...parts });

describe("isOutsideZone", () => {
  it("is inside up to and including the edge, measured from the middle of the head", () => {
    // A head at 1245 has its middle at 1252.5: 292.5 from the centre.
    expect(isOutsideZone({ x: 1245, y: 532.5 }, zone())).toBe(false);
    expect(isOutsideZone({ x: 1260, y: 532.5 }, zone())).toBe(true);
  });

  it("goes the short way round the world that wraps", () => {
    const edge = zone({ position: { x: 20, y: 540 }, radius: 60 });

    expect(isOutsideZone({ x: 1900, y: 532.5 }, edge, room)).toBe(false);
    expect(isOutsideZone({ x: 1800, y: 532.5 }, edge, room)).toBe(true);
  });
});

describe("zoneWarning", () => {
  it("is null without a zone", () => {
    expect(zoneWarning(frame({ snakes: [snake(0, 0)] }), "me")).toBeNull();
  });

  it("says the phase and the time left in it", () => {
    const warning = zoneWarning(frame({ zone: zone({ phase: "warning", msLeft: 9000 }) }), "me");

    expect(warning).toMatchObject({ phase: "warning", msLeft: 9000 });
  });

  it("says the own snake is outside once the zone is closing", () => {
    const outside = zoneWarning(frame({ zone: zone(), snakes: [snake(100, 100)] }), "me");
    const inside = zoneWarning(frame({ zone: zone(), snakes: [snake(950, 530)] }), "me");

    expect(outside?.outside).toBe(true);
    expect(inside?.outside).toBe(false);
  });

  it("holds the snake to nothing while the zone only warns", () => {
    expect(zoneWarning(frame({ zone: zone({ phase: "warning", radius: 2000 }), snakes: [snake(100, 100)] }), "me")?.outside).toBe(false);
    expect(zoneWarning(frame({ zone: zone({ phase: "warning" }), snakes: [snake(100, 100)] }), "me")?.outside).toBe(false);
  });

  it("is outside while the zone holds too", () => {
    expect(zoneWarning(frame({ zone: zone({ phase: "holding", radius: 160 }), snakes: [snake(100, 100)] }), "me")?.outside).toBe(true);
  });

  it("is not outside without a living own snake", () => {
    expect(zoneWarning(frame({ zone: zone() }), "me")?.outside).toBe(false);
    expect(zoneWarning(frame({ zone: zone(), snakes: [snake(100, 100, { isAlive: false })] }), "me")?.outside).toBe(false);
    expect(zoneWarning(frame({ zone: zone(), snakes: [snake(100, 100, { guid: "other" })] }), "me")?.outside).toBe(false);
  });
});
