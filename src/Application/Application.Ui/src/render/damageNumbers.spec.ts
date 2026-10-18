import { describe, expect, it } from "vitest";
import type { FloatingDamage } from "@/types/game";
import { DAMAGE_LIFETIME_MS, damageColor, damageLabel, damagePosition, glyphFor, isVisible, recentDamage } from "./damageNumbers";

const hit = (overrides: Partial<FloatingDamage> = {}): FloatingDamage => ({
  shooterGuid: "me",
  targetGuid: "them",
  x: 200,
  y: 300,
  damage: 23.4,
  kind: "hit",
  bornMs: 1000,
  ...overrides,
});

describe("damageLabel", () => {
  it("is the damage as a whole number", () => {
    expect(damageLabel(hit({ damage: 23.4 }))).toBe("23");
    expect(damageLabel(hit({ damage: 23.6 }))).toBe("24");
  });

  it("is at least 1, so a hit is never shown as nothing", () => {
    expect(damageLabel(hit({ damage: 0.2 }))).toBe("1");
  });

  it("is KO for a kill", () => {
    expect(damageLabel(hit({ kind: "kill", damage: 0 }))).toBe("KO");
  });
});

describe("glyphFor", () => {
  it.each([..."0123456789KO"])("has a 3x5 picture for %s", (character) => {
    const rows = glyphFor(character);

    expect(rows).toHaveLength(5);
    expect(rows.every((row) => row.length === 3)).toBe(true);
    expect(rows.join("")).toContain("#");
  });

  it("has nothing for a character it does not know", () => {
    expect(glyphFor("?")).toEqual([]);
  });
});

describe("damageColor", () => {
  it("is gold for damage you dealt", () => {
    expect(damageColor(hit(), "me")).toBe("#ffd23f");
  });

  it("is red for damage you took", () => {
    expect(damageColor(hit(), "them")).toBe("#ff5a5a");
  });

  it("is white for someone else's fight you can still see", () => {
    expect(damageColor(hit(), "other")).toBe("#ffffff");
  });
});

describe("damagePosition", () => {
  it("starts where the segment was hit and floats up", () => {
    const start = damagePosition(hit(), 1000);
    const later = damagePosition(hit(), 1000 + DAMAGE_LIFETIME_MS / 2);

    expect(start).toEqual({ x: 200, y: 300 });
    expect(later.x).toBe(200);
    expect(later.y).toBeLessThan(300);
  });
});

describe("isVisible", () => {
  it("shows for a while after the hit", () => {
    expect(isVisible(hit(), 1000)).toBe(true);
    expect(isVisible(hit(), 1000 + DAMAGE_LIFETIME_MS - 1)).toBe(true);
  });

  it("is gone after its lifetime", () => {
    expect(isVisible(hit(), 1000 + DAMAGE_LIFETIME_MS)).toBe(false);
  });

  it("is not shown before it was born", () => {
    expect(isVisible(hit(), 900)).toBe(false);
  });
});

describe("recentDamage", () => {
  it("keeps what is still on screen and drops the rest", () => {
    const old = hit({ bornMs: 0 });
    const fresh = hit({ bornMs: 5000 });

    expect(recentDamage([old, fresh], 5100)).toEqual([fresh]);
  });
});
