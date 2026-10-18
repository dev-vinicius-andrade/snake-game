<script setup lang="ts">
import { computed } from "vue";
import { getText } from "@/helpers";
import type { PrizeCompass } from "@/render/prizeCompass";

const props = defineProps<{ compass: PrizeCompass | null }>();

/** The arrow glyph points right at angle 0; rotating by the angle points it at the prize. */
const arrowStyle = computed(() => ({ transform: `rotate(${props.compass?.angle ?? 0}rad)` }));
const meters = computed(() => Math.round((props.compass?.distance ?? 0) / 10));
</script>

<template>
  <div v-if="compass" class="prize-hint" role="status">
    <span class="prize-arrow" :style="arrowStyle" aria-hidden="true">&#10148;</span>
    <span>{{ getText({ key: "game.prize" }) }} {{ meters }}</span>
  </div>
</template>

<style scoped>
.prize-hint {
  position: fixed;
  z-index: 10;
  top: 8px;
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
.prize-arrow {
  display: inline-block;
  font-size: 18px;
}
</style>
