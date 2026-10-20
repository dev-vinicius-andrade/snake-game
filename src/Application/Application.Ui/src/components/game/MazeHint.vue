<script setup lang="ts">
import { computed } from "vue";
import { getText } from "@/helpers";
import type { MazeWarning } from "@/render/maze";

const props = defineProps<{ warning: MazeWarning | null }>();

const text = computed(() => {
  const warning = props.warning;
  if (!warning) return "";
  return getText({ key: warning.inside ? "game.maze.inside" : "game.maze.race", params: { seconds: Math.ceil(warning.msLeft / 1000) } });
});
</script>

<template>
  <div v-if="warning" class="maze-hint" :class="{ 'maze-inside': warning.inside }" role="status" aria-live="polite">
    <span aria-hidden="true">&#9638;</span>
    <span>{{ text }}</span>
  </div>
</template>

<style scoped>
.maze-hint {
  position: fixed;
  z-index: 10;
  top: 236px;
  left: 50%;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 10px;
  color: #c9ceff;
  font-family: monospace;
  font-size: 14px;
  background: rgba(11, 13, 23, 0.78);
  border: 2px solid #4c5070;
  transform: translateX(-50%);
  pointer-events: none;
}
/* The own snake is in the maze: the walls cost it mass, so the banner goes warmer. */
.maze-inside {
  color: #ffe27a;
  border-color: #b8860b;
}
</style>
