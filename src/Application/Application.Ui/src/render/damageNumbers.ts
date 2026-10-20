import type { FloatingDamage, GamePoint } from "@/types/game";

/** How long a damage number stays on screen. */
export const DAMAGE_LIFETIME_MS = 900;
/** How far, in world units, it floats up over its lifetime. */
const RISE_WORLD_UNITS = 40;

const DEALT_COLOR = "#ffd23f";
const TAKEN_COLOR = "#ff5a5a";
const OTHER_COLOR = "#ffffff";
const SAVED_COLOR = "#6bff8a";

/** A 3x5 pixel font: `#` is a lit pixel. Only what damage labels need. */
const GLYPHS: Readonly<Record<string, readonly string[]>> = {
  "0": ["###", "#.#", "#.#", "#.#", "###"],
  "1": [".#.", "##.", ".#.", ".#.", "###"],
  "2": ["###", "..#", "###", "#..", "###"],
  "3": ["###", "..#", "###", "..#", "###"],
  "4": ["#.#", "#.#", "###", "..#", "..#"],
  "5": ["###", "#..", "###", "..#", "###"],
  "6": ["###", "#..", "###", "#.#", "###"],
  "7": ["###", "..#", ".#.", ".#.", ".#."],
  "8": ["###", "#.#", "###", "#.#", "###"],
  "9": ["###", "#.#", "###", "..#", "###"],
  "-": ["...", "...", "###", "...", "..."],
  K: ["#.#", "#.#", "##.", "#.#", "#.#"],
  U: ["#.#", "#.#", "#.#", "#.#", "###"],
  P: ["###", "#.#", "###", "#..", "#.."],
  O: ["###", "#.#", "#.#", "#.#", "###"],
};

export const GLYPH_WIDTH = 3;
export const GLYPH_HEIGHT = 5;

export const glyphFor = (character: string): readonly string[] => GLYPHS[character] ?? [];

/** The text of a damage number: the damage as a whole number, at least 1, or KO for a kill, or a minus and the mass lost. */
export function damageLabel(damage: FloatingDamage): string {
  if (damage.kind === "kill") return "KO";
  if (damage.kind === "saved") return "1UP";
  if (damage.kind === "lost") return `-${Math.max(1, Math.round(damage.damage))}`;
  return String(Math.max(1, Math.round(damage.damage)));
}

/** Gold for damage you dealt, red for damage you took, white for anyone else's. */
export function damageColor(damage: FloatingDamage, selfGuid: string | null | undefined): string {
  if (damage.kind === "saved") return SAVED_COLOR;
  if (damage.shooterGuid === selfGuid) return DEALT_COLOR;
  return damage.targetGuid === selfGuid ? TAKEN_COLOR : OTHER_COLOR;
}

export const isVisible = (damage: FloatingDamage, nowMs: number): boolean =>
  nowMs >= damage.bornMs && nowMs - damage.bornMs < DAMAGE_LIFETIME_MS;

/** The damage numbers that are still worth keeping at `nowMs`. */
export const recentDamage = (all: readonly FloatingDamage[], nowMs: number): FloatingDamage[] =>
  all.filter((damage) => nowMs - damage.bornMs < DAMAGE_LIFETIME_MS);

/** Where the number is at `nowMs`: where the segment was hit, floating up. */
export function damagePosition(damage: FloatingDamage, nowMs: number): GamePoint {
  const progress = Math.min(1, Math.max(0, (nowMs - damage.bornMs) / DAMAGE_LIFETIME_MS));
  return { x: damage.x, y: damage.y - RISE_WORLD_UNITS * progress };
}
