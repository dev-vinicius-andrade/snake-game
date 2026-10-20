import { beforeEach, describe, expect, it } from "vitest";
import { FakeSoundEngine } from "@/test/fakeAudio";
import type { BossSnapshot, FloatingDamage, FoodSnapshot, GameSnapshot, MazeSnapshot, MineSnapshot, ShotSnapshot, SnakeSnapshot, StrikeSnapshot } from "@/types/game";
import { SoundDirector } from "./soundDirector";

const room = { guid: null, width: 4000, height: 4000 };
const color = { fillColor: "#00ff00", border: "#00ff00" };

function snake(guid: string, x: number, y: number, overrides: Partial<SnakeSnapshot> = {}): SnakeSnapshot {
  const head = { x, y };
  return {
    guid,
    connectionId: guid,
    name: guid,
    head,
    path: [head, head, head, head],
    segmentHealth: [1, 1, 1, 1],
    isAlive: true,
    isBoosting: false,
    perks: [],
    perkTiers: [],
    autoFire: false,
    foodsEaten: 0,
    score: 0,
    color,
    direction: "right",
    ...overrides,
  };
}

const food = (guid: string, x: number, y: number, kind: FoodSnapshot["kind"] = "normal"): FoodSnapshot => ({ guid, kind, position: { x, y }, color, value: 1 });
const shot = (guid: string, ownerGuid: string): ShotSnapshot => ({ guid, position: { x: 0, y: 0 }, size: 5, ownerGuid });
const strike = (guid: string, kind: StrikeSnapshot["kind"]): StrikeSnapshot => ({ guid, kind, position: { x: 100, y: 100 }, radius: 50, msLeft: 100 });
const mine = (guid: string): MineSnapshot => ({ guid, position: { x: 900, y: 900 }, radius: 60, msToArm: 0 });
const maze: MazeSnapshot = { position: { x: 1000, y: 1000 }, columns: 4, rows: 4, corridor: 3, twistiness: 50, seed: 1, unit: 15, msLeft: 60000 };
// 4 cells of 3 blocks plus a wall: 17 blocks of 15 units, so the maze covers 1000..1255.
const insideMaze = { x: 1100, y: 1100 };

function frame(overrides: Partial<GameSnapshot> = {}): GameSnapshot {
  return { room, snakes: [snake("me", 100, 100)], foods: [], shots: [], strikes: [], boss: null, zone: null, ...overrides };
}

const hit = (overrides: Partial<FloatingDamage>): FloatingDamage => ({ shooterGuid: "other", targetGuid: "me", x: 0, y: 0, damage: 1, kind: "hit", bornMs: 0, ...overrides });

describe("SoundDirector", () => {
  let engine: FakeSoundEngine;
  let director: SoundDirector;
  const watch = (...snapshots: GameSnapshot[]) => {
    snapshots.forEach((snapshot) => director.observe(snapshot, "me"));
    return engine.take();
  };

  beforeEach(() => {
    engine = new FakeSoundEngine();
    director = new SoundDirector(engine);
  });

  it("makes no sound for the first snapshot, whatever it holds", () => {
    expect(watch(frame({ mines: [mine("m")], showerMsLeft: 5000, bounty: { guid: "me", msLeft: 1, rewardMass: 1 } }))).toEqual([]);
  });

  it("makes no sound while nothing changes", () => {
    expect(watch(frame(), frame(), frame())).toEqual([]);
  });

  describe("eating", () => {
    it("plays eat when the own snake eats a food", () => {
      expect(watch(frame(), frame({ snakes: [snake("me", 100, 100, { foodsEaten: 1 })] }))).toEqual(["eat"]);
    });
    it("plays prize when the food that vanished next to the head was a prize", () => {
      const before = frame({ foods: [food("p", 110, 100, "prize")] });
      const after = frame({ snakes: [snake("me", 100, 100, { foodsEaten: 1 })] });
      expect(watch(before, after)).toEqual(["prize"]);
    });
    it("plays the maze prize sound when that prize was in a maze", () => {
      const before = frame({ maze, foods: [food("p", 110, 100, "prize")] });
      const after = frame({ maze, snakes: [snake("me", 100, 100, { foodsEaten: 1 })] });
      expect(watch(before, after)).toEqual(["mazePrize"]);
    });
    it("does not count a prize eaten by someone else", () => {
      const before = frame({ foods: [food("p", 2000, 2000, "prize")], snakes: [snake("me", 100, 100), snake("other", 2000, 2000)] });
      const after = frame({ snakes: [snake("me", 100, 100), snake("other", 2000, 2000)] });
      expect(watch(before, after)).toEqual([]);
    });
    it("is quiet when the food counter falls (a new life)", () => {
      expect(watch(frame({ snakes: [snake("me", 100, 100, { foodsEaten: 5 })] }), frame())).toEqual([]);
    });
  });

  it("plays shoot when a new shot of the own snake appears, once, and not for the shots of others", () => {
    expect(watch(frame(), frame({ shots: [shot("a", "me"), shot("b", "me")] }))).toEqual(["shoot"]);
    expect(watch(frame({ shots: [shot("a", "me")] }), frame({ shots: [shot("a", "me"), shot("c", "other")] }))).toEqual([]);
  });

  it("plays boost when the own snake starts boosting, not while it goes on", () => {
    const boosting = frame({ snakes: [snake("me", 100, 100, { isBoosting: true })] });
    expect(watch(frame(), boosting)).toEqual(["boost"]);
    expect(watch(boosting)).toEqual([]);
  });

  describe("world events", () => {
    it("plays lightning and meteor when a warning is gone (the strike fell), one per kind", () => {
      const before = frame({ strikes: [strike("a", "lightning"), strike("b", "lightning"), strike("c", "meteor")] });
      expect(watch(before, frame({ strikes: [strike("b", "lightning")] }))).toEqual(["lightning", "meteor"]);
    });
    it("plays nothing for a strike that is still coming", () => {
      expect(watch(frame({ strikes: [strike("a", "lightning")] }), frame({ strikes: [strike("a", "lightning")] }))).toEqual([]);
    });
    it("plays mine when a mine is gone", () => {
      expect(watch(frame({ mines: [mine("a"), mine("b")] }), frame({ mines: [mine("b")] }))).toEqual(["mine"]);
    });
    it("plays bountyStart when a bounty appears, and bountyClaimed when the wanted snake died", () => {
      const wanted = { guid: "other", msLeft: 9000, rewardMass: 50 };
      const others = [snake("me", 100, 100), snake("other", 500, 500)];
      expect(watch(frame({ snakes: others }), frame({ snakes: others, bounty: wanted }))).toEqual(["bountyStart"]);
      director.reset();
      watch(frame({ snakes: others, bounty: wanted }));
      expect(watch(frame({ snakes: [snake("me", 100, 100)] }))).toEqual(["bountyClaimed"]);
    });
    it("is quiet when a bounty merely runs out", () => {
      const wanted = { guid: "other", msLeft: 9000, rewardMass: 50 };
      const others = [snake("me", 100, 100), snake("other", 500, 500)];
      expect(watch(frame({ snakes: others, bounty: wanted }), frame({ snakes: others }))).toEqual([]);
    });
    it("plays shower and rush when they begin, not while they last", () => {
      expect(watch(frame(), frame({ showerMsLeft: 8000 }), frame({ showerMsLeft: 7000 }))).toEqual(["shower"]);
      expect(watch(frame({ rushMsLeft: 0 }), frame({ rushMsLeft: 8000 }), frame({ rushMsLeft: 7000 }))).toEqual(["rush"]);
    });
  });

  describe("maze", () => {
    const inMaze = (overrides: Partial<SnakeSnapshot> = {}) => frame({ maze, snakes: [snake("me", insideMaze.x, insideMaze.y, overrides)] });
    it("plays mazeOpen when a maze rises", () => {
      expect(watch(frame(), frame({ maze }))).toEqual(["mazeOpen"]);
    });
    it("plays mazeEnter when the own head crosses into the maze", () => {
      expect(watch(frame({ maze }), inMaze())).toEqual(["mazeEnter"]);
      expect(watch(inMaze())).toEqual([]);
    });
    it("plays mazeWall when the own snake shrinks inside the maze without boosting", () => {
      const shorter = { path: [{ x: 1, y: 1 }, { x: 1, y: 1 }] };
      expect(watch(inMaze(), inMaze(shorter))).toEqual(["mazeWall"]);
    });
    it("does not call a boost shrink a wall", () => {
      const shorter = { path: [{ x: 1, y: 1 }, { x: 1, y: 1 }], isBoosting: true };
      expect(watch(inMaze(), inMaze(shorter))).toEqual(["boost"]);
    });
    it("does not call a shrink outside the maze a wall", () => {
      const shorter = { path: [{ x: 1, y: 1 }] };
      expect(watch(frame({ maze }), frame({ maze, snakes: [snake("me", 100, 100, shorter)] }))).toEqual([]);
    });
  });

  describe("damage", () => {
    it("plays hurt when the own snake is hit or loses mass, broke when a segment breaks", () => {
      director.observe(frame(), "me");
      director.damage([hit({ kind: "hit" }), hit({ kind: "lost", shooterGuid: "" }), hit({ kind: "broke" })], "me");
      expect(engine.take()).toEqual(["hurt", "hurt", "broke"]);
    });
    it("plays hit when the own shot lands, kill when it kills", () => {
      director.damage([hit({ shooterGuid: "me", targetGuid: "other", kind: "hit" }), hit({ shooterGuid: "me", targetGuid: "other", kind: "kill" })], "me");
      expect(engine.take()).toEqual(["hit", "kill"]);
    });
    it("plays turretHit when the target is the boss", () => {
      const boss: BossSnapshot = { guid: "boss", position: { x: 0, y: 0 }, size: 50, hpRatio: 1, angle: 0, arms: 3 };
      director.observe(frame({ boss }), "me");
      director.damage([hit({ shooterGuid: "me", targetGuid: "boss", kind: "hit" })], "me");
      expect(engine.take()).toEqual(["turretHit"]);
    });
    it("stays quiet for the kill that ends the own snake (died does that)", () => {
      director.damage([hit({ kind: "kill" })], "me");
      expect(engine.take()).toEqual([]);
    });
    it("plays each damage event once, however often the list is handed over", () => {
      const events = [hit({ kind: "hit" })];
      director.damage(events, "me");
      director.damage([...events, hit({ kind: "broke" })], "me");
      expect(engine.take()).toEqual(["hurt", "broke"]);
    });
    it("ignores damage between other snakes", () => {
      director.damage([hit({ shooterGuid: "a", targetGuid: "b" })], "me");
      expect(engine.take()).toEqual([]);
    });
  });

  it("plays die", () => {
    director.died();
    expect(engine.take()).toEqual(["die"]);
  });

  it("starts over after a reset: the next snapshot is only remembered", () => {
    director.observe(frame(), "me");
    director.reset();
    expect(watch(frame({ showerMsLeft: 5000 }))).toEqual([]);
  });
});
