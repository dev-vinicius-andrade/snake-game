<script setup lang="ts">
import { computed } from "vue";
import { getText } from "@/helpers";
import type { LightningWarning } from "@/render/lightning";

const props = defineProps<{ warning: LightningWarning | null }>();

const text = computed(() =>
  getText({ key: props.warning?.danger ? "game.lightning.danger" : "game.lightning.warning" })
);
</script>

<template>
  <div v-if="warning" class="lightning-hint" :class="{ 'lightning-danger': warning.danger }" role="alert" aria-live="assertive">
    <span aria-hidden="true">&#9889;</span>
    <span>{{ text }}</span>
  </div>
</template>

<style scoped>
.lightning-hint {
  position: fixed;
  z-index: 10;
  top: 46px;
  left: 50%;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 10px;
  color: #ffe14d;
  font-family: monospace;
  font-size: 14px;
  background: rgba(11, 13, 23, 0.78);
  border: 2px solid #8a7a1f;
  transform: translateX(-50%);
  pointer-events: none;
}
.lightning-danger {
  color: #ffffff;
  background: rgba(150, 20, 20, 0.85);
  border-color: #ff5a5a;
  animation: lightning-blink 0.35s steps(2, jump-none) infinite;
}
@keyframes lightning-blink {
  50% {
    background: rgba(11, 13, 23, 0.85);
  }
}
@media (prefers-reduced-motion: reduce) {
  .lightning-danger {
    animation: none;
  }
}
</style>
