import { describe, expect, it } from "vitest";
import type { FoodSnapshot, GameSnapshot, SnakeSnapshot } from "@/types/game";
import { prizeCompass } from "./prizeCompass";

const room = { guid: null, width: 1920, height: 1080 };

const snake = (x: number, y: number, overrides: Partial<SnakeSnapshot> = {}): SnakeSnapshot => ({
  guid: "me",
  connectionId: "c",
  name: "ana",
  head: { x, y },
  path: [{ x, y }],
  segmentHealth: [1],
  isAlive: true,
  isBoosting: false,
  perks: [],
  autoFire: false,
  foodsEaten: 0,
  score: 0,
  color: { fillColor: "#0f0", border: "#070" },
  direction: "right",
  ...overrides,
});

const food = (x: number, y: number, kind: "normal" | "prize" = "prize"): FoodSnapshot => ({
  guid: `f${x}${y}${kind}`,
  position: { x, y },
  color: { fillColor: "#fd3", border: "#a80" },
  value: 20,
  kind,
});

const frame = (snakes: SnakeSnapshot[], foods: FoodSnapshot[]): GameSnapshot => ({ room, snakes, foods, shots: [] });

describe("prizeCompass", () => {
  it("points at the prize and says how far it is", () => {
    const compass = prizeCompass(frame([snake(100, 100)], [food(400, 500)]), "me")!;

    expect(compass.angle).toBeCloseTo(Math.atan2(400, 300), 6);
    expect(compass.distance).toBeCloseTo(500, 6);
  });

  it("goes the short way round the world", () => {
    const compass = prizeCompass(frame([snake(1900, 540)], [food(20, 540)]), "me")!;

    expect(compass.angle).toBeCloseTo(0, 6);
    expect(compass.distance).toBeCloseTo(40, 6);
  });

  it("ignores ordinary food", () => {
    expect(prizeCompass(frame([snake(0, 0)], [food(10, 10, "normal")]), "me")).toBeNull();
  });

  it("picks the nearest prize", () => {
    const compass = prizeCompass(frame([snake(0, 0)], [food(800, 0), food(100, 0)]), "me")!;

    expect(compass.distance).toBeCloseTo(100, 6);
  });

  it("is null without a living own snake", () => {
    expect(prizeCompass(frame([snake(0, 0, { isAlive: false })], [food(10, 10)]), "me")).toBeNull();
    expect(prizeCompass(frame([snake(0, 0)], [food(10, 10)]), "someone else")).toBeNull();
    expect(prizeCompass(frame([snake(0, 0)], [food(10, 10)]), null)).toBeNull();
  });
});
