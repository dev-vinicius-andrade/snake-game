import type { AbilitySnapshot } from "@/types/game";

/** A cooldown perk's state for the list: ready, recharging or on, with the whole seconds left. */
export interface Cooldown {
  state: "ready" | "recharging" | "active";
  seconds: number;
}

export function cooldownOf(id: string, abilities: readonly AbilitySnapshot[] | undefined): Cooldown | null {
  const ability = abilities?.find((candidate) => candidate.id === id);
  if (!ability) return null;
  if (ability.activeMs > 0) return { state: "active", seconds: Math.ceil(ability.activeMs / 1000) };
  if (ability.readyMs > 0) return { state: "recharging", seconds: Math.ceil(ability.readyMs / 1000) };
  return { state: "ready", seconds: 0 };
}

/**
 * What tells one set of perks from another. Every snapshot brings new arrays, so the list compares these strings to know
 * whether it has anything to work out again.
 */
export const perksKey = (perks: readonly string[], tiers: readonly number[] | undefined): string => `${perks.join(",")}|${(tiers ?? []).join(",")}`;

/** Changes when a cooldown changes what it shows (its state or its whole seconds), not with every snapshot. */
export const cooldownKey = (abilities: readonly AbilitySnapshot[] | undefined): string =>
  (abilities ?? []).map(({ id }) => `${id}:${cooldownOf(id, abilities)?.state}:${cooldownOf(id, abilities)?.seconds}`).join("|");
