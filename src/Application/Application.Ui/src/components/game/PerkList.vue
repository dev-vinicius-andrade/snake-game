<script setup lang="ts">
import { computed } from "vue";
import type { CatalogPerk } from "@/composables/usePerkCatalog";
import type { AbilitySnapshot } from "@/types/game";
import { perkLabel } from "@/game/perks";
import { perkIcon, perkIconOf, perkParams, perkTooltipKey, tierInfo, tierLabel } from "@/game/perkInfo";
import GameIcon from "@/components/game/GameIcon.vue";
import { getText } from "@/helpers";

const props = defineProps<{
  /** Ids of the perks the snake has, in the order it earned them. */
  perks: string[];
  /** The tier of each of `perks`, in the same order. Missing: the first for all. */
  tiers?: number[];
  /** What each perk is and does. Without it a perk is shown by its id. */
  catalog: CatalogPerk[];
  /** The cooldown perks of the snake, with the time left to recharge and to run out. */
  abilities?: AbilitySnapshot[];
  /** Extra lives the snake can still be saved with. */
  lives?: number;
}>();

interface Shown {
  id: string;
  icon: string;
  title: string;
  /** One sentence for each thing the perk does. */
  lines: string[];
  /** The roman numeral of the tier, only for a perk that has gone past the first. */
  badge: string;
  /** For a cooldown perk: ready, recharging or on, with the whole seconds left. */
  cooldown: { state: "ready" | "recharging" | "active"; seconds: number } | null;
}

function cooldownOf(id: string): Shown["cooldown"] {
  const ability = props.abilities?.find((candidate) => candidate.id === id);
  if (!ability) return null;
  if (ability.activeMs > 0) return { state: "active", seconds: Math.ceil(ability.activeMs / 1000) };
  if (ability.readyMs > 0) return { state: "recharging", seconds: Math.ceil(ability.readyMs / 1000) };
  return { state: "ready", seconds: 0 };
}

function cooldownLine(cooldown: Shown["cooldown"]): string[] {
  if (!cooldown) return [];
  const key = cooldown.state === "ready" ? "game.abilityReady" : cooldown.state === "active" ? "game.abilityActive" : "game.abilityRecharging";
  return [getText({ key, params: { seconds: cooldown.seconds } })];
}

function describe(id: string, index: number): Shown {
  const perk = props.catalog.find((candidate) => candidate.id === id);
  const tier = props.tiers?.[index] ?? 1;
  const cooldown = cooldownOf(id);
  if (!perk) return { id, icon: perkIcon(""), title: perkLabel(id), lines: [], badge: tier > 1 ? tierLabel(tier) : "", cooldown };
  const { multiplier, nextFoods, nextRewardOnly } = tierInfo(perk, tier);
  const hasTiers = perk.tiers.length > 0;
  return {
    id,
    icon: perkIconOf(perk),
    title: hasTiers ? `${perk.name || perkLabel(id)} ${tierLabel(tier)}` : perk.name || perkLabel(id),
    lines: [
      ...(perk.description?.trim() ? [perk.description.trim()] : []),
      ...perk.effects.map((entry) =>
        getText({
          key: perkTooltipKey(entry.effect, entry.target),
          params: {
            ...perkParams(
              { ...entry, foodsRequired: perk.foodsRequired, cooldownSeconds: perk.cooldownSeconds, durationSeconds: perk.durationSeconds },
              multiplier
            ),
            event: entry.target ? getText({ key: `game.events.${entry.target}` }) : "",
          },
        })
      ),
      ...(nextFoods === null ? [] : [getText({ key: "game.perkNextTier", params: { foods: nextFoods } })]),
      ...(nextRewardOnly ? [getText({ key: "game.perkRewardTier" })] : []),
      ...cooldownLine(cooldown),
    ],
    badge: tier > 1 ? tierLabel(tier) : "",
    cooldown,
  };
}

const shown = computed(() => props.perks.map((id, index) => describe(id, index)));
</script>

<template>
  <section v-if="shown.length > 0 || (lives ?? 0) > 0" class="perk-list" :aria-label="getText({ key: 'game.perks' })">
    <span v-if="(lives ?? 0) > 0" class="lives" role="img" :aria-label="`${getText({ key: 'game.lives' })}: ${lives}`">
      <GameIcon name="game-icons:heart-plus" :size="18" />
      <span class="lives-count" aria-hidden="true">{{ lives }}</span>
    </span>
    <VTooltip v-for="perk in shown" :key="perk.id" location="top">
      <template #activator="{ props: activator }">
        <span
          class="perk"
          :class="perk.cooldown && `perk-${perk.cooldown.state}`"
          tabindex="0"
          :aria-label="`${perk.title}. ${perk.lines.join('. ')}`"
          v-bind="activator"
        >
          <GameIcon :name="perk.icon" :size="18" />
          <span v-if="perk.badge" class="perk-tier" aria-hidden="true">{{ perk.badge }}</span>
          <span v-if="perk.cooldown && perk.cooldown.state !== 'ready'" class="perk-seconds" aria-hidden="true">{{ perk.cooldown.seconds }}</span>
        </span>
      </template>
      <div class="perk-title">{{ perk.title }}</div>
      <div v-for="line in perk.lines" :key="line">{{ line }}</div>
    </VTooltip>
  </section>
</template>

<style scoped>
/* Bottom centre: the top corners already hold the name, the room code and the ranking. */
.perk-list {
  position: fixed;
  z-index: 10;
  bottom: 8px;
  left: 50%;
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 4px;
  width: max-content;
  max-width: min(60vw, 520px);
  transform: translateX(-50%);
}
.perk-tier {
  position: absolute;
  right: -4px;
  bottom: -6px;
  padding: 0 2px;
  color: #ffd23f;
  font-family: monospace;
  font-size: 9px;
  line-height: 11px;
  background: #1b1405;
  border: 1px solid #b8860b;
}
.perk {
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  color: #1b1405;
  background: #ffd23f;
  border: 2px solid #b8860b;
  cursor: default;
}
.perk:focus-visible {
  outline: 2px solid #ffffff;
}
.perk-title {
  font-weight: bold;
}
.perk-recharging {
  color: #6b6b6b;
  background: #3a3a3a;
  border-color: #555555;
}
.perk-active {
  background: #7fe9ff;
  border-color: #2a8fa8;
}
.perk-ready {
  border-color: #ffffff;
}
.perk-seconds {
  position: absolute;
  top: -6px;
  left: -4px;
  padding: 0 2px;
  color: #ffffff;
  font-family: monospace;
  font-size: 9px;
  line-height: 11px;
  background: #0b0d17;
  border: 1px solid #555555;
}
.lives {
  position: relative;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  color: #ffffff;
  background: #c0392b;
  border: 2px solid #7b241c;
}
.lives-count {
  position: absolute;
  right: -4px;
  bottom: -6px;
  padding: 0 2px;
  color: #ffffff;
  font-family: monospace;
  font-size: 9px;
  line-height: 11px;
  background: #1b0505;
  border: 1px solid #7b241c;
}
</style>
