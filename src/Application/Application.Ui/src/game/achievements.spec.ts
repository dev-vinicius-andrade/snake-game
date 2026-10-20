import { describe, expect, it } from "vitest";
import {
  achievementProgress,
  achievementText,
  buildAchievementViews,
  formatPlayTime,
  mergeAchievementTexts,
  parseUnlockedIds,
  sortAchievements,
  type AchievementDefinition,
} from "./achievements";

const kills: AchievementDefinition = { id: "hunter", icon: "game-icons:crosshair", stat: "kills", threshold: 25 };

describe("achievementProgress", () => {
  it("is the share of the threshold reached", () => {
    expect(achievementProgress({ kills: 5 }, kills)).toEqual({ value: 5, threshold: 25, percent: 20, unlocked: false });
  });
  it("caps at 100 and unlocks at the threshold", () => {
    expect(achievementProgress({ kills: 80 }, kills)).toMatchObject({ percent: 100, unlocked: true });
    expect(achievementProgress({ kills: 25 }, kills)).toMatchObject({ percent: 100, unlocked: true });
  });
  it("treats a missing stat as zero", () => {
    expect(achievementProgress({}, kills)).toMatchObject({ value: 0, percent: 0, unlocked: false });
  });
  it("stays unlocked when the unlock is known", () => {
    expect(achievementProgress({ kills: 1 }, kills, true)).toMatchObject({ percent: 100, unlocked: true });
  });
  it("does not round up to done", () => {
    expect(achievementProgress({ kills: 24 }, kills).percent).toBe(96);
  });
});

describe("sortAchievements", () => {
  const defs: AchievementDefinition[] = [
    { id: "a", icon: "i", stat: "kills", threshold: 10 },
    { id: "b", icon: "i", stat: "kills", threshold: 100 },
    { id: "c", icon: "i", stat: "foods", threshold: 10 },
    { id: "d", icon: "i", stat: "foods", threshold: 100 },
  ];
  it("puts unlocked first by date, then the closest to done", () => {
    const views = buildAchievementViews(
      defs,
      [
        { id: "d", unlockedAt: "2026-01-02T00:00:00Z" },
        { id: "c", unlockedAt: "2026-01-01T00:00:00Z" },
      ],
      { kills: 5, foods: 20 }
    );
    expect(sortAchievements(views).map((view) => view.id)).toEqual(["c", "d", "a", "b"]);
  });
});

describe("mergeAchievementTexts", () => {
  const base = { en: { hunter: { name: "Snake Hunter", description: "Kill 25 snakes." } }, pt: { hunter: { name: "Caçadora", description: "Derrote 25 cobras." } } };

  it("puts the texts an administrator wrote over the built-in ones, by language", () => {
    const merged = mergeAchievementTexts(base, [
      { id: "snacker", icon: "x", stat: "foods", threshold: 10, texts: { en: { name: "Snacker", description: "Eat 10 fruits." } } },
      { id: "hunter", icon: "x", stat: "kills", threshold: 25, texts: { en: { name: "Big Hunter", description: "" } } },
    ]);

    expect(achievementText(merged, "en", "snacker").name).toBe("Snacker");
    expect(achievementText(merged, "en", "hunter").name).toBe("Big Hunter");
    expect(achievementText(merged, "pt", "hunter").name).toBe("Caçadora");
    expect(achievementText(merged, "pt", "snacker").name).toBe("Snacker");
  });

  it("changes nothing for definitions with no texts, and does not touch what it was given", () => {
    const merged = mergeAchievementTexts(base, [{ id: "hunter", icon: "x", stat: "kills", threshold: 25 }]);

    expect(merged).toEqual(base);
    mergeAchievementTexts(base, [{ id: "n", icon: "x", stat: "kills", threshold: 1, texts: { en: { name: "N", description: "" } } }]);
    expect(base.en).not.toHaveProperty("n");
  });
});

describe("achievementText", () => {
  const texts = { en: { x: { name: "X", description: "dx" } }, pt: { x: { name: "Xis", description: "dxis" } } };
  it("uses the locale, then english, then the id", () => {
    expect(achievementText(texts, "pt", "x").name).toBe("Xis");
    expect(achievementText(texts, "fr", "x").name).toBe("X");
    expect(achievementText(texts, "pt", "y")).toEqual({ name: "y", description: "" });
  });
});

describe("formatPlayTime", () => {
  it("splits seconds", () => {
    expect(formatPlayTime(3725)).toEqual({ hours: 1, minutes: 2, seconds: 5 });
    expect(formatPlayTime(-4)).toEqual({ hours: 0, minutes: 0, seconds: 0 });
  });
});

describe("parseUnlockedIds", () => {
  it("reads the ids and drops anything else", () => {
    expect(parseUnlockedIds({ ids: ["a", 3, "", "b"] })).toEqual(["a", "b"]);
    expect(parseUnlockedIds(null)).toEqual([]);
    expect(parseUnlockedIds({ ids: "a" })).toEqual([]);
  });
});
