import { describe, expect, it } from "vitest";
import type { GameSnapshot } from "@/types/game";
import { timedEvents } from "./timedEvents";

const room = { guid: null, width: 1920, height: 1080 };
const frame = (parts: Partial<GameSnapshot>): GameSnapshot => ({ room, snakes: [], foods: [], shots: [], strikes: [], boss: null, zone: null, ...parts });

describe("timedEvents", () => {
  it("is nothing while neither a fruit shower nor a sugar rush is going", () => {
    expect(timedEvents(frame({}))).toEqual([]);
    expect(timedEvents(frame({ showerMsLeft: 0, rushMsLeft: 0 }))).toEqual([]);
  });

  it("says how long is left of each, the shower first, both at once", () => {
    expect(timedEvents(frame({ showerMsLeft: 12000, rushMsLeft: 3000 }))).toEqual([
      { kind: "shower", msLeft: 12000 },
      { kind: "rush", msLeft: 3000 },
    ]);
  });

  it("has only the one that is going", () => {
    expect(timedEvents(frame({ rushMsLeft: 3000 }))).toEqual([{ kind: "rush", msLeft: 3000 }]);
  });
});
