<script setup lang="ts">
import { useElementSize } from "@vueuse/core";
import { computed, ref } from "vue";
import { useCanvasRenderer } from "@/composables/useCanvasRenderer";
import { followCamera, viewLayout, type Camera } from "@/render/camera";
import { DEFAULT_HEAD_SIZE } from "@/render/pixelRenderer";
import { headCenter, type StageRect } from "@/render/stageGeometry";
import type { GamePoint, GameSnapshot } from "@/types/game";

const props = defineProps<{
  snapshot: GameSnapshot | null;
  selfGuid?: string | null;
}>();

const canvas = ref<HTMLCanvasElement | null>(null);
const { width, height } = useElementSize(canvas);
const layout = computed(() => viewLayout(width.value, height.value, { world: props.snapshot?.room }));

/** The camera follows the own snake. When it died, or before it appears, it stays where it last was. */
let lastTarget: GamePoint | null = null;

function target(snapshot: GameSnapshot): GamePoint {
  const own = snapshot.snakes.find((snake) => snake.guid === props.selfGuid && snake.isAlive);
  if (own) lastTarget = headCenter(own.head, DEFAULT_HEAD_SIZE);
  return lastTarget ?? { x: snapshot.room.width / 2, y: snapshot.room.height / 2 };
}

/** Where the camera is right now, or null until there is a snapshot and the screen has a size. */
function currentCamera(): Camera | null {
  const snapshot = props.snapshot;
  return snapshot && layout.value ? followCamera(layout.value, snapshot.room, target(snapshot)) : null;
}

/** Where the canvas is on the page, so pointer positions can be turned into positions on the screen. */
function getRect(): StageRect | null {
  const box = canvas.value?.getBoundingClientRect();
  return box ? { left: box.left, top: box.top, width: box.width, height: box.height } : null;
}

defineExpose({ getRect, getCamera: currentCamera });

useCanvasRenderer(canvas, () => {
  const camera = currentCamera();
  return {
    snapshot: props.snapshot,
    options: { selfGuid: props.selfGuid, origin: camera?.origin, view: camera?.view },
  };
});
</script>

<template>
  <canvas ref="canvas" class="game-stage" />
</template>

<style scoped>
/* The camera fills the screen; the HUD floats above it. */
.game-stage {
  position: fixed;
  inset: 0;
  display: block;
  width: 100vw;
  height: 100vh;
  height: 100dvh;
  background: #07080f;
  image-rendering: pixelated;
}
</style>
