<script setup lang="ts">
import { computed } from "vue";
import { getText } from "@/helpers";
import type { ZoneWarning } from "@/render/zone";

const props = defineProps<{ warning: ZoneWarning | null }>();

const text = computed(() => {
  const warning = props.warning;
  if (!warning) return "";
  if (warning.outside) return getText({ key: "game.zone.outside" });
  if (warning.phase === "warning") return getText({ key: "game.zone.warning", params: { seconds: Math.ceil(warning.msLeft / 1000) } });
  return getText({ key: warning.phase === "closing" ? "game.zone.closing" : "game.zone.holding" });
});
</script>

<template>
  <div v-if="warning?.outside" class="zone-vignette" aria-hidden="true" />
  <div v-if="warning" class="zone-hint" :class="{ 'zone-outside': warning.outside }" role="status" aria-live="polite">
    <span aria-hidden="true">&#9678;</span>
    <span>{{ text }}</span>
  </div>
</template>

<style scoped>
.zone-hint {
  position: fixed;
  z-index: 10;
  top: 122px;
  left: 50%;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 10px;
  color: #5fd0ff;
  font-family: monospace;
  font-size: 14px;
  background: rgba(11, 13, 23, 0.78);
  border: 2px solid #2a6f8f;
  transform: translateX(-50%);
  pointer-events: none;
}
.zone-outside {
  color: #ffffff;
  background: rgba(150, 20, 40, 0.85);
  border-color: #ff5a6e;
}
/* A red glow round the edges of the screen while the own snake is outside and losing mass. */
.zone-vignette {
  position: fixed;
  z-index: 9;
  inset: 0;
  box-shadow: inset 0 0 90px 20px rgba(220, 30, 60, 0.55);
  pointer-events: none;
}
</style>
