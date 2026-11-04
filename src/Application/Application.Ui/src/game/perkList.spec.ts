import { describe, expect, it } from "vitest";
import { cooldownKey, cooldownOf, perksKey } from "./perkList";

describe("perksKey", () => {
  it("is the same for the same perks and tiers in new arrays", () => {
    expect(perksKey(["magnet", "dash"], [1, 2])).toBe(perksKey(["magnet", "dash"], [1, 2]));
  });

  it("differs when a perk is earned or goes up a tier", () => {
    const before = perksKey(["magnet"], [1]);

    expect(perksKey(["magnet", "dash"], [1, 1])).not.toBe(before);
    expect(perksKey(["magnet"], [2])).not.toBe(before);
  });

  it("reads missing tiers as none", () => {
    expect(perksKey(["magnet"], undefined)).toBe(perksKey(["magnet"], []));
  });
});

describe("cooldownOf", () => {
  const ability = (activeMs: number, readyMs: number) => ({ id: "dash", activeMs, readyMs });

  it("is null for a perk with no cooldown", () => {
    expect(cooldownOf("dash", [])).toBeNull();
    expect(cooldownOf("dash", undefined)).toBeNull();
  });

  it("is active while it runs, recharging while it waits, ready after", () => {
    expect(cooldownOf("dash", [ability(1500, 9000)])).toEqual({ state: "active", seconds: 2 });
    expect(cooldownOf("dash", [ability(0, 4100)])).toEqual({ state: "recharging", seconds: 5 });
    expect(cooldownOf("dash", [ability(0, 0)])).toEqual({ state: "ready", seconds: 0 });
  });
});

describe("cooldownKey", () => {
  const ability = (activeMs: number, readyMs: number) => ({ id: "dash", activeMs, readyMs });

  it("does not change while the time left moves inside the same second", () => {
    expect(cooldownKey([ability(0, 4100)])).toBe(cooldownKey([ability(0, 4020)]));
  });

  it("changes when a second goes by or the state does", () => {
    expect(cooldownKey([ability(0, 4100)])).not.toBe(cooldownKey([ability(0, 3900)]));
    expect(cooldownKey([ability(0, 100)])).not.toBe(cooldownKey([ability(0, 0)]));
  });

  it("is empty with no abilities", () => {
    expect(cooldownKey(undefined)).toBe("");
  });
});
