import { describe, expect, it } from "vitest";
import en from "@/locales/messages/en";
import pt from "@/locales/messages/pt";
import { CREDIT_GROUPS, CREDITS } from "./credits";

describe("credits", () => {
  it("gives every entry a name, who made it, a licence and an https address", () => {
    for (const credit of CREDITS) {
      expect(credit.name, credit.name).toBeTruthy();
      expect(credit.by, credit.name).toBeTruthy();
      expect(credit.license, credit.name).toBeTruthy();
      expect(credit.url, credit.name).toMatch(/^https:\/\//);
    }
  });

  it("lists each thing once, in a known group", () => {
    expect(new Set(CREDITS.map((credit) => credit.name)).size).toBe(CREDITS.length);
    for (const credit of CREDITS) expect(CREDIT_GROUPS, credit.name).toContain(credit.group);
  });

  it("credits the game icons as their licence asks: who made them, the licence and where to find them", () => {
    const icons = CREDITS.find((credit) => credit.name === "game-icons.net");

    expect(icons?.by).toMatch(/Lorc/);
    expect(icons?.by).toMatch(/Delapouite/);
    expect(icons?.license).toBe("CC BY 3.0");
    expect(icons?.licenseUrl).toBe("https://creativecommons.org/licenses/by/3.0/");
  });

  it("every group has a title in both languages", () => {
    for (const group of CREDIT_GROUPS) {
      expect((en.credits.groups as Record<string, string>)[group], `en ${group}`).toBeTruthy();
      expect((pt.credits.groups as Record<string, string>)[group], `pt ${group}`).toBeTruthy();
    }
  });
});
