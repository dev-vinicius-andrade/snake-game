<script setup lang="ts">
import { computed } from "vue";
import type { CatalogPerk } from "@/composables/usePerkCatalog";
import { perkLabel } from "@/game/perks";
import { perkIcon, perkParams, perkTooltipKey } from "@/game/perkInfo";
import { getText } from "@/helpers";

const props = defineProps<{
  /** Ids of the perks the snake has, in the order it earned them. */
  perks: string[];
  /** What each perk is and does. Without it a perk is shown by its id. */
  catalog: CatalogPerk[];
}>();

interface Shown {
  id: string;
  icon: string;
  title: string;
  /** One sentence for each thing the perk does. */
  lines: string[];
}

function describe(id: string): Shown {
  const perk = props.catalog.find((candidate) => candidate.id === id);
  if (!perk) return { id, icon: perkIcon(""), title: perkLabel(id), lines: [] };
  return {
    id,
    icon: perkIcon(perk.effects[0]?.effect ?? ""),
    title: perk.name || perkLabel(id),
    lines: perk.effects.map((entry) =>
      getText({ key: perkTooltipKey(entry.effect), params: perkParams({ ...entry, foodsRequired: perk.foodsRequired }) })
    ),
  };
}

const shown = computed(() => props.perks.map(describe));
</script>

<template>
  <section v-if="shown.length > 0" class="perk-list" :aria-label="getText({ key: 'game.perks' })">
    <VTooltip v-for="perk in shown" :key="perk.id" location="top">
      <template #activator="{ props: activator }">
        <span class="perk" tabindex="0" :aria-label="`${perk.title}. ${perk.lines.join('. ')}`" v-bind="activator">
          <VIcon size="small">{{ perk.icon }}</VIcon>
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
.perk {
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
