import { describe, expect, it } from "vitest";
import type { GameSnapshot, MineSnapshot, SnakeSnapshot } from "@/types/game";
import { BLAST_MS, MineTracker, isMineNear } from "./mines";

const room = { guid: null, width: 1920, height: 1080 };

const mine = (x: number, y: number, msToArm = 0, radius = 60): MineSnapshot => ({ guid: `${x},${y}`, position: { x, y }, radius, msToArm });

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

const frame = (snakes: SnakeSnapshot[]): GameSnapshot => ({ room, snakes, foods: [], shots: [], strikes: [], boss: null, zone: null });

describe("MineTracker", () => {
  it("shows every mine where it lies, with its blast radius", () => {
    const tracker = new MineTracker();
    tracker.observe([mine(100, 200), mine(700, 300, 0, 80)], 0);

    expect(tracker.view(0).mines).toEqual([
      { center: { x: 100, y: 200 }, radius: 60, armed: true },
      { center: { x: 700, y: 300 }, radius: 80, armed: true },
    ]);
  });

  it("counts the arming down between snapshots, and follows the server when a newer one arrives", () => {
    const tracker = new MineTracker();
    tracker.observe([mine(100, 200, 1000)], 0);

    expect(tracker.view(0).mines[0].armed).toBe(false);
    expect(tracker.view(999).mines[0].armed).toBe(false);
    expect(tracker.view(1000).mines[0].armed).toBe(true);

    tracker.observe([mine(100, 200, 800)], 500);
    expect(tracker.view(1000).mines[0].armed).toBe(false);
    expect(tracker.view(1300).mines[0].armed).toBe(true);
  });

  it("blows a mine up where it lay when the next snapshot no longer has it", () => {
    const tracker = new MineTracker();
    tracker.observe([mine(100, 200), mine(700, 300)], 0);
    tracker.observe([mine(700, 300)], 500);

    const view = tracker.view(500);
    expect(view.mines.map((mark) => mark.center)).toEqual([{ x: 700, y: 300 }]);
    expect(view.blasts).toEqual([{ center: { x: 100, y: 200 }, radius: 60, age: 0 }]);
    expect(tracker.view(500 + BLAST_MS / 2).blasts[0].age).toBeCloseTo(0.5);
    expect(tracker.view(500 + BLAST_MS).blasts).toEqual([]);
  });

  it("does not blow up a mine that is still there", () => {
    const tracker = new MineTracker();
    tracker.observe([mine(100, 200, 900)], 0);
    tracker.observe([mine(100, 200, 500)], 400);

    expect(tracker.view(400).blasts).toEqual([]);
  });

  it("forgets everything on clear, without a blast", () => {
    const tracker = new MineTracker();
    tracker.observe([mine(100, 200)], 0);
    tracker.clear();
    tracker.observe([], 100);

    expect(tracker.view(100)).toEqual({ mines: [], blasts: [] });
  });
});

describe("isMineNear", () => {
  const mark = { center: { x: 500, y: 400 }, radius: 60, armed: true };

  it("is near when a living head is within twice the blast radius, measured from the middle of the head", () => {
    // A head at 600 has its middle at 607.5: 107.5 from the mine.
    expect(isMineNear(mark, frame([snake(600, 392.5)]))).toBe(true);
    expect(isMineNear(mark, frame([snake(620, 392.5)]))).toBe(false);
  });

  it("counts any snake but the dead, and goes the short way round the world", () => {
    expect(isMineNear(mark, frame([snake(520, 392.5, { guid: "other" })]))).toBe(true);
    expect(isMineNear(mark, frame([snake(520, 392.5, { isAlive: false })]))).toBe(false);
    expect(isMineNear({ ...mark, center: { x: 10, y: 400 } }, frame([snake(1900, 392.5)]))).toBe(true);
  });
});
