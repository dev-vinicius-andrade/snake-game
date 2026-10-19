<script setup lang="ts">
import { computed } from "vue";
import type { CatalogPerk } from "@/composables/usePerkCatalog";
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
}>();

interface Shown {
  id: string;
  icon: string;
  title: string;
  /** One sentence for each thing the perk does. */
  lines: string[];
  /** The roman numeral of the tier, only for a perk that has gone past the first. */
  badge: string;
}

function describe(id: string, index: number): Shown {
  const perk = props.catalog.find((candidate) => candidate.id === id);
  const tier = props.tiers?.[index] ?? 1;
  if (!perk) return { id, icon: perkIcon(""), title: perkLabel(id), lines: [], badge: tier > 1 ? tierLabel(tier) : "" };
  const { multiplier, nextFoods } = tierInfo(perk, tier);
  const hasTiers = perk.tiers.length > 0;
  return {
    id,
    icon: perkIconOf(perk),
    title: hasTiers ? `${perk.name || perkLabel(id)} ${tierLabel(tier)}` : perk.name || perkLabel(id),
    lines: [
      ...(perk.description?.trim() ? [perk.description.trim()] : []),
      ...perk.effects.map((entry) =>
        getText({ key: perkTooltipKey(entry.effect), params: perkParams({ ...entry, foodsRequired: perk.foodsRequired }, multiplier) })
      ),
      ...(nextFoods === null ? [] : [getText({ key: "game.perkNextTier", params: { foods: nextFoods } })]),
    ],
    badge: tier > 1 ? tierLabel(tier) : "",
  };
}

const shown = computed(() => props.perks.map((id, index) => describe(id, index)));
</script>

<template>
  <section v-if="shown.length > 0" class="perk-list" :aria-label="getText({ key: 'game.perks' })">
    <VTooltip v-for="perk in shown" :key="perk.id" location="top">
      <template #activator="{ props: activator }">
        <span class="perk" tabindex="0" :aria-label="`${perk.title}. ${perk.lines.join('. ')}`" v-bind="activator">
          <GameIcon :name="perk.icon" :size="18" />
          <span v-if="perk.badge" class="perk-tier" aria-hidden="true">{{ perk.badge }}</span>
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
</style>
