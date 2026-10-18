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
  pointer-events: none;
}
.perk {
  padding: 1px 5px;
  color: #1b1405;
  font-family: monospace;
  font-size: 11px;
  background: #ffd23f;
  border: 2px solid #b8860b;
}
</style>
