import { describe, expect, it } from "vitest";
import type { BountySnapshot, GameSnapshot, SnakeSnapshot } from "@/types/game";
import { bountyWarning, wantedSnake } from "./bounty";

const room = { guid: null, width: 1920, height: 1080 };

function snake(guid: string, name: string, overrides: Partial<SnakeSnapshot> = {}): SnakeSnapshot {
  const head = { x: 100, y: 100 };
  return {
    guid,
    connectionId: "c",
    name,
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

const bounty = (guid: string, msLeft = 45000, rewardMass = 150): BountySnapshot => ({ guid, msLeft, rewardMass });
const frame = (parts: Partial<GameSnapshot>): GameSnapshot => ({ room, snakes: [], foods: [], shots: [], strikes: [], boss: null, zone: null, ...parts });

describe("wantedSnake", () => {
  it("is the living snake the bounty is on", () => {
    const wanted = snake("w", "bob");

    expect(wantedSnake(frame({ snakes: [snake("me", "ana"), wanted], bounty: bounty("w") }))).toBe(wanted);
  });

  it("is nobody without a bounty, or when that snake is dead or gone", () => {
    expect(wantedSnake(frame({ snakes: [snake("w", "bob")] }))).toBeNull();
    expect(wantedSnake(frame({ snakes: [snake("w", "bob", { isAlive: false })], bounty: bounty("w") }))).toBeNull();
    expect(wantedSnake(frame({ snakes: [snake("x", "bob")], bounty: bounty("w") }))).toBeNull();
  });
});

describe("bountyWarning", () => {
  it("names the wanted snake, the reward and the time left", () => {
    const warning = bountyWarning(frame({ snakes: [snake("me", "ana"), snake("w", "bob")], bounty: bounty("w", 30000, 200) }), "me");

    expect(warning).toEqual({ name: "bob", msLeft: 30000, rewardMass: 200, self: false });
  });

  it("says when the own snake is the wanted one", () => {
    expect(bountyWarning(frame({ snakes: [snake("me", "ana")], bounty: bounty("me") }), "me")?.self).toBe(true);
  });

  it("is null without a bounty, or without a living wanted snake", () => {
    expect(bountyWarning(frame({ snakes: [snake("me", "ana")] }), "me")).toBeNull();
    expect(bountyWarning(frame({ snakes: [snake("me", "ana")], bounty: bounty("gone") }), "me")).toBeNull();
  });
});
