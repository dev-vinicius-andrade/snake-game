<script setup lang="ts">
import { computed, ref } from "vue";
import { useCanvasRenderer } from "@/composables/useCanvasRenderer";
import type { GameSnapshot } from "@/types/game";

const props = defineProps<{
  snapshot: GameSnapshot | null;
  selfGuid?: string | null;
}>();

const canvas = ref<HTMLCanvasElement | null>(null);

const aspectRatio = computed(() => {
  const room = props.snapshot?.room;
  return room ? `${room.width} / ${room.height}` : "16 / 9";
});

useCanvasRenderer(canvas, () => ({
  snapshot: props.snapshot,
  options: { selfGuid: props.selfGuid },
}));
</script>

<template>
  <canvas ref="canvas" class="game-stage" :style="{ aspectRatio }" />
</template>

<style scoped>
.game-stage {
  display: block;
  width: 100%;
  max-height: 100vh;
  margin: 0 auto;
  background: #16161d;
  image-rendering: pixelated;
}
</style>
