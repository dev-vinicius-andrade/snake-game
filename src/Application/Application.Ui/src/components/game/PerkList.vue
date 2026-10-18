<script setup lang="ts">
import { getText } from "@/helpers";
import { perkLabel } from "@/game/perks";

defineProps<{ perks: string[]; foodsEaten: number }>();
</script>

<template>
  <section v-if="perks.length > 0" class="perk-list" :aria-label="getText({ key: 'game.perks' })">
    <span v-for="perk in perks" :key="perk" class="perk">{{ perkLabel(perk) }}</span>
  </section>
</template>

<style scoped>
.perk-list {
  position: fixed;
  z-index: 10;
  top: 64px;
  left: 8px;
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  max-width: 46vw;
  pointer-events: none;
}
.perk {
  padding: 2px 6px;
  color: #1b1405;
  font-family: monospace;
  font-size: 12px;
  background: #ffd23f;
  border: 2px solid #b8860b;
}
</style>
