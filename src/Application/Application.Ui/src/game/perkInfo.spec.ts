import { describe, expect, it } from "vitest";
import en from "@/locales/messages/en";
import pt from "@/locales/messages/pt";
import { PERK_EFFECTS, PERK_ICON_CHOICES, perkIcon, perkIconOf, perkParams, perkTooltipKey } from "./perkInfo";

describe("perkInfo", () => {
  it("knows an icon for every effect the server has, and a fallback for others", () => {
    for (const effect of PERK_EFFECTS) expect(perkIcon(effect)).toMatch(/^mdi-/);
    expect(perkIcon("SomethingNew")).toBe("mdi-star-four-points");
  });

  it("shows the icon a perk was given, else the icon of its first effect", () => {
    expect(perkIconOf({ icon: "mdi-skull", effects: [{ effect: "Magnet" }] })).toBe("mdi-skull");
    expect(perkIconOf({ icon: "", effects: [{ effect: "Magnet" }] })).toBe(perkIcon("Magnet"));
    expect(perkIconOf({ effects: [{ effect: "Armor" }] })).toBe(perkIcon("Armor"));
    expect(perkIconOf({ effects: [] })).toBe("mdi-star-four-points");
  });

  it("ignores an icon that is not a Material Design Icons name", () => {
    expect(perkIconOf({ icon: "skull", effects: [{ effect: "Magnet" }] })).toBe(perkIcon("Magnet"));
    expect(perkIconOf({ icon: "mdi-x y", effects: [] })).toBe("mdi-star-four-points");
  });

  it("offers only valid, different icon names to pick from", () => {
    expect(new Set(PERK_ICON_CHOICES).size).toBe(PERK_ICON_CHOICES.length);
    for (const name of PERK_ICON_CHOICES) expect(name).toMatch(/^mdi-[a-z0-9]+(-[a-z0-9]+)*$/);
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
