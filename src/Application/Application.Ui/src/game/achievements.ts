/** Pure helpers for stats and achievements (docs/ACHIEVEMENTS.md). */

export interface AchievementDefinition {
  id: string;
  /** A game-icons name such as `game-icons:snake`. */
  icon: string;
  stat: string;
  threshold: number;
}

export interface AchievementUnlock {
  id: string;
  /** ISO date string from the manager. */
  unlockedAt: string;
}

export interface AchievementProgress {
  value: number;
  threshold: number;
  /** 0 to 100, whole number. */
  percent: number;
  unlocked: boolean;
}

export interface AchievementView extends AchievementProgress {
  id: string;
  icon: string;
  stat: string;
  unlockedAt: string | null;
}

export type AchievementTexts = Record<string, Record<string, { name: string; description: string }>>;

export const STAT_KEYS = [
  "games",
  "deaths",
  "kills",
  "foods",
  "prizes",
  "bossKills",
  "lightningHits",
  "meteorHits",
  "playSeconds",
  "bestScore",
  "longestSnake",
  "bestKillsInLife",
] as const;

export type StatKey = (typeof STAT_KEYS)[number];

export function achievementProgress(
  stats: Readonly<Record<string, number>>,
  definition: AchievementDefinition,
  unlocked = false
): AchievementProgress {
  const value = Math.max(0, stats[definition.stat] ?? 0);
  const threshold = definition.threshold;
  const reached = unlocked || value >= threshold;
  const ratio = threshold > 0 ? value / threshold : 1;
  return { value, threshold, percent: reached ? 100 : Math.min(100, Math.floor(ratio * 100)), unlocked: reached };
}

export function buildAchievementViews(
  definitions: readonly AchievementDefinition[],
  unlocks: readonly AchievementUnlock[],
  stats: Readonly<Record<string, number>>
): AchievementView[] {
  const when = new Map(unlocks.map((unlock) => [unlock.id, unlock.unlockedAt]));
  return definitions.map((definition) => {
    const unlockedAt = when.get(definition.id) ?? null;
    return {
      id: definition.id,
      icon: definition.icon,
      stat: definition.stat,
      unlockedAt,
      ...achievementProgress(stats, definition, unlockedAt !== null),
    };
  });
}

/** Unlocked first (oldest unlock first), then the ones closest to done. */
export function sortAchievements(views: readonly AchievementView[]): AchievementView[] {
  return [...views].sort((a, b) => {
    if (a.unlocked !== b.unlocked) return a.unlocked ? -1 : 1;
    if (a.unlocked) return Date.parse(a.unlockedAt ?? "") - Date.parse(b.unlockedAt ?? "") || 0;
    return b.percent - a.percent;
  });
}

/** Name and description for a locale; falls back to English, then to the id. */
export function achievementText(texts: AchievementTexts, locale: string, id: string): { name: string; description: string } {
  return texts[locale]?.[id] ?? texts.en?.[id] ?? { name: id, description: "" };
}

export function formatPlayTime(totalSeconds: number): { hours: number; minutes: number; seconds: number } {
  const whole = Math.max(0, Math.floor(totalSeconds));
  return { hours: Math.floor(whole / 3600), minutes: Math.floor((whole % 3600) / 60), seconds: whole % 60 };
}

/** The ids of the hub event `AchievementsUnlocked` ({ ids }); anything odd gives an empty list. */
export function parseUnlockedIds(raw: unknown): string[] {
  const list = (typeof raw === "object" && raw !== null ? ((raw as Record<string, unknown>).ids ?? (raw as Record<string, unknown>).Ids) : null) ?? [];
  return Array.isArray(list) ? list.filter((id): id is string => typeof id === "string" && id.length > 0) : [];
}
