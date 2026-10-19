<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref } from "vue";
import type { FramePerf } from "@/render/framePerf";

/** The frame meter's numbers on screen, for finding out where the game stutters. Toggled with F3. */
const props = defineProps<{ perf: FramePerf }>();

const lines = ref<string[]>([]);
let timer = 0;

function refresh() {
  lines.value = props.perf.summary(performance.now()).split(" | ");
}

onMounted(() => {
  refresh();
  timer = window.setInterval(refresh, 500);
});
onBeforeUnmount(() => window.clearInterval(timer));
</script>

<template>
  <div class="perf-overlay" role="status" aria-live="off">
    <div v-for="line in lines" :key="line">{{ line }}</div>
  </div>
</template>

<style scoped>
.perf-overlay {
  position: fixed;
  left: 8px;
  bottom: 8px;
  z-index: 20;
  padding: 6px 8px;
  font: 11px/1.35 ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
  color: #b7f5c8;
  background: rgba(7, 8, 15, 0.78);
  border: 1px solid var(--snake-line);
  pointer-events: none;
}
</style>
