<script setup lang="ts">
import { computed } from "vue";
import { getText } from "@/helpers";
import type { BossCompass } from "@/render/boss";

const props = defineProps<{ compass: BossCompass | null }>();

/** The arrow glyph points right at angle 0; rotating by the angle points it at the boss. */
const arrowStyle = computed(() => ({ transform: `rotate(${props.compass?.angle ?? 0}rad)` }));
const percent = computed(() => Math.round((props.compass?.hpRatio ?? 0) * 100));
const meters = computed(() => Math.round((props.compass?.distance ?? 0) / 10));
</script>

<template>
  <div v-if="compass" class="boss-hint" role="status">
    <span class="boss-arrow" :style="arrowStyle" aria-hidden="true">&#10148;</span>
    <span>{{ getText({ key: "game.boss.name" }) }} {{ percent }}% &middot; {{ meters }}</span>
  </div>
</template>

<style scoped>
.boss-hint {
  position: fixed;
  z-index: 10;
  top: 84px;
  left: 50%;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 10px;
  color: #ff9ab0;
  font-family: monospace;
  font-size: 14px;
  background: rgba(11, 13, 23, 0.78);
  border: 2px solid #7d3a55;
  transform: translateX(-50%);
  pointer-events: none;
}
.boss-arrow {
  display: inline-block;
  font-size: 18px;
}
</style>
