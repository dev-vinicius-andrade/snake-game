import { describe, expect, it } from "vitest";
import en from "@/locales/messages/en";
import pt from "@/locales/messages/pt";
import { PERK_EFFECTS, PERK_ICON_CHOICES, perkIcon, perkIconOf, perkParams, perkTooltipKey, tierInfo, tierLabel } from "./perkInfo";
import { gameIconBody } from "./gameIcons";

describe("perkInfo", () => {
  it("knows an icon for every effect the server has, from the game icons, and a fallback for others", () => {
    for (const effect of PERK_EFFECTS) expect(perkIcon(effect)).toMatch(/^game-icons:/);
    expect(perkIcon("SomethingNew")).toBe("game-icons:sparkles");
  });

  it("gives every effect its own icon", () => {
    expect(new Set(PERK_EFFECTS.map((effect) => perkIcon(effect))).size).toBe(PERK_EFFECTS.length);
  });

  it("shows the icon a perk was given, else the icon of its first effect", () => {
    expect(perkIconOf({ icon: "mdi-skull", effects: [{ effect: "Magnet" }] })).toBe("mdi-skull");
    expect(perkIconOf({ icon: "", effects: [{ effect: "Magnet" }] })).toBe(perkIcon("Magnet"));
    expect(perkIconOf({ effects: [{ effect: "Armor" }] })).toBe(perkIcon("Armor"));
    expect(perkIconOf({ effects: [] })).toBe("game-icons:sparkles");
  });

  it("shows an icon of the game icons too", () => {
    expect(perkIconOf({ icon: "game-icons:crown", effects: [{ effect: "Magnet" }] })).toBe("game-icons:crown");
  });

  it("ignores an icon that is not a Material Design Icons or game icons name", () => {
    expect(perkIconOf({ icon: "skull", effects: [{ effect: "Magnet" }] })).toBe(perkIcon("Magnet"));
    expect(perkIconOf({ icon: "mdi-x y", effects: [] })).toBe("game-icons:sparkles");
    expect(perkIconOf({ icon: "game-icons:Crown", effects: [] })).toBe("game-icons:sparkles");
    expect(perkIconOf({ icon: "game-icons:", effects: [] })).toBe("game-icons:sparkles");
  });

  it("offers only valid, different game icons to pick from, every one of which can be drawn", () => {
    expect(new Set(PERK_ICON_CHOICES).size).toBe(PERK_ICON_CHOICES.length);
    for (const name of PERK_ICON_CHOICES) {
      expect(name).toMatch(/^game-icons:[a-z0-9]+(-[a-z0-9]+)*$/);
      expect(gameIconBody(name), name).toBeTruthy();
    }
  });

  it("has a name, a help text and a tooltip for every effect in both languages", () => {
    for (const messages of [en, pt]) {
      for (const effect of PERK_EFFECTS) {
        const text = (messages as any).perkEffects[effect];
        expect(text?.name, effect).toBeTruthy();
        expect(text?.help, effect).toBeTruthy();
        expect(text?.tooltip, effect).toBeTruthy();
      }
    }
  });

  it("points at the tooltip text of an effect", () => {
    expect(perkTooltipKey("Magnet")).toBe("perkEffects.Magnet.tooltip");
  });

  it("turns an amount into the numbers a tooltip talks about", () => {
    expect(perkParams({ effect: "FasterReload", amount: 0.35, foodsRequired: 25 })).toMatchObject({ amount: 0.35, percent: 35, foods: 25 });
    expect(perkParams({ effect: "ShotSpeed", amount: 0.5, foodsRequired: 12 }).percent).toBe(50);
    expect(perkParams({ effect: "BurstShot", amount: 2, foodsRequired: 30 }).count).toBe(3);
    expect(perkParams({ effect: "ExtraShots", amount: 1, foodsRequired: 15 }).count).toBe(2);
  });

  it("rounds odd amounts so a tooltip stays readable", () => {
    expect(perkParams({ effect: "Armor", amount: 0.3333333, foodsRequired: 20 }).percent).toBe(33);
    expect(perkParams({ effect: "BoostSpeed", amount: 0.25000001, foodsRequired: 8 }).amount).toBe(0.25);
  });
});

describe("tierLabel", () => {
  it("is the roman numeral of the tier", () => {
    expect([1, 2, 3, 4, 5].map(tierLabel)).toEqual(["I", "II", "III", "IV", "V"]);
    expect(tierLabel(7)).toBe("7");
  });
});

describe("tierInfo", () => {
  const perk = { foodsRequired: 6, tiers: [{ foodsRequired: 24, multiplier: 2 }, { foodsRequired: 12, multiplier: 1.5 }] };

  it("says what a tier multiplies by and what the next one asks for, whatever order the tiers came in", () => {
    expect(tierInfo(perk, 1)).toEqual({ multiplier: 1, nextFoods: 12 });
    expect(tierInfo(perk, 2)).toEqual({ multiplier: 1.5, nextFoods: 24 });
    expect(tierInfo(perk, 3)).toEqual({ multiplier: 2, nextFoods: null });
  });

  it("is the first tier for a perk that has none, or a tier that does not exist", () => {
    expect(tierInfo({ foodsRequired: 6 }, 1)).toEqual({ multiplier: 1, nextFoods: null });
    expect(tierInfo(perk, 9)).toEqual({ multiplier: 2, nextFoods: null });
    expect(tierInfo(perk, 0)).toEqual({ multiplier: 1, nextFoods: 12 });
  });
});

describe("perkParams with a tier", () => {
  it("scales the amount by the tier", () => {
    expect(perkParams({ effect: "Magnet", amount: 60, foodsRequired: 6 }, 1.5)).toMatchObject({ amount: 90, foods: 6 });
    expect(perkParams({ effect: "ExtraShots", amount: 1, foodsRequired: 6 }, 2)).toMatchObject({ count: 3 });
  });

  it("keeps a ratio under what the game allows", () => {
    expect(perkParams({ effect: "Armor", amount: 0.6, foodsRequired: 6 }, 4)).toMatchObject({ amount: 0.95, percent: 95 });
  });

  it("is what it always was without a tier", () => {
    expect(perkParams({ effect: "Magnet", amount: 60, foodsRequired: 6 })).toMatchObject({ amount: 60 });
  });
});
