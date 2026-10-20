import { describe, expect, it } from "vitest";
import { SOUND_IDS, SOUNDS, soundDuration } from "./sounds";

describe("sound recipes", () => {
  it("has a recipe for every id", () => {
    for (const id of SOUND_IDS) expect(SOUNDS[id].length, id).toBeGreaterThan(0);
  });
  it("keeps every note audible and short", () => {
    for (const id of SOUND_IDS)
      for (const note of SOUNDS[id]) {
        expect(note.duration, id).toBeGreaterThan(0);
        expect(note.gain, id).toBeGreaterThan(0);
        expect(note.gain, id).toBeLessThanOrEqual(1);
        expect(note.start, id).toBeGreaterThanOrEqual(0);
        if (note.wave !== "noise") expect(note.from, id).toBeGreaterThan(0);
      }
    for (const id of SOUND_IDS) expect(soundDuration(id), id).toBeLessThan(2);
  });
  it("ends when the last note ends", () => {
    expect(soundDuration("eat")).toBeCloseTo(Math.max(...SOUNDS.eat.map((n) => n.start + n.duration)));
  });
});
