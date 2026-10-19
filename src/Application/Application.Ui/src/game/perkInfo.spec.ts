import { describe, expect, it } from "vitest";
import en from "@/locales/messages/en";
import pt from "@/locales/messages/pt";
import { PERK_EFFECTS, perkIcon, perkParams, perkTooltipKey } from "./perkInfo";

describe("perkInfo", () => {
  it("knows an icon for every effect the server has, and a fallback for others", () => {
    for (const effect of PERK_EFFECTS) expect(perkIcon(effect)).toMatch(/^mdi-/);
    expect(perkIcon("SomethingNew")).toBe("mdi-star-four-points");
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
