<script setup lang="ts">
import { computed } from "vue";
import { getText } from "@/helpers";
import type { BountyWarning } from "@/render/bounty";

const props = defineProps<{ warning: BountyWarning | null }>();

const text = computed(() => {
  const warning = props.warning;
  if (!warning) return "";
  const params = { name: warning.name, mass: Math.round(warning.rewardMass), seconds: Math.ceil(warning.msLeft / 1000) };
  return getText({ key: warning.self ? "game.bounty.self" : "game.bounty.other", params });
});
</script>

<template>
  <div v-if="warning" class="bounty-hint" :class="{ 'bounty-self': warning.self }" role="status" aria-live="polite">
    <span aria-hidden="true">&#9819;</span>
    <span>{{ text }}</span>
  </div>
</template>

<style scoped>
.bounty-hint {
  position: fixed;
  z-index: 10;
  top: 160px;
  left: 50%;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 10px;
  color: #ffd23f;
  font-family: monospace;
  font-size: 14px;
  background: rgba(11, 13, 23, 0.78);
  border: 2px solid #b8860b;
  transform: translateX(-50%);
  pointer-events: none;
}
/* The own snake is the wanted one: everyone is after it. */
.bounty-self {
  color: #ffffff;
  background: rgba(150, 20, 20, 0.85);
  border-color: #ff5a5a;
}
</style>
