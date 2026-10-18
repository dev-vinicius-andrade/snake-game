<script setup lang="ts">
import { useElementSize } from "@vueuse/core";
import { computed, ref, watch } from "vue";
import { useCanvasRenderer } from "@/composables/useCanvasRenderer";
import { followCamera, viewLayout, type Camera } from "@/render/camera";
import { DEFAULT_HEAD_SIZE } from "@/render/pixelRenderer";
import { CameraSmoother } from "@/render/cameraSmoothing";
import { SnapshotBuffer } from "@/render/snapshotBuffer";
import { headCenter, type StageRect } from "@/render/stageGeometry";
import type { FloatingDamage, GamePoint, GameSnapshot } from "@/types/game";

const props = defineProps<{
  snapshot: GameSnapshot | null;
  selfGuid?: string | null;
  damage?: FloatingDamage[];
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

/**
 * Server snapshots come in about 30 times a second; the screen is drawn faster. What is drawn is the world a moment in
 * the past, blended between the two snapshots around that time, so motion is smooth and a turn does not shake the screen.
 */
const buffer = new SnapshotBuffer({ jitterCorrection: 0.1 });
/** The camera follows the snake a little behind, so quickly turning left and right does not shake the screen. */
const smoother = new CameraSmoother();
watch(
  () => props.snapshot,
  (snapshot) => (snapshot ? buffer.push(snapshot, performance.now()) : buffer.clear()),
  { immediate: true }
);

interface Frame {
  snapshot: GameSnapshot;
  camera: Camera | null;
}

/** What to draw now, and where the camera is. Null until there is a snapshot. */
function currentFrame(): Frame | null {
  const snapshot = buffer.sample(performance.now());
  if (!snapshot) return null;
  if (!layout.value) return { snapshot, camera: null };
  const focus = target(snapshot);
  const center = smoother.step(focus, performance.now(), snapshot.room);
  return { snapshot, camera: followCamera(layout.value, snapshot.room, focus, center) };
}

const currentCamera = (): Camera | null => currentFrame()?.camera ?? null;

/** Where the canvas is on the page, so pointer positions can be turned into positions on the screen. */
function getRect(): StageRect | null {
  const box = canvas.value?.getBoundingClientRect();
  return box ? { left: box.left, top: box.top, width: box.width, height: box.height } : null;
}

defineExpose({ getRect, getCamera: currentCamera });

useCanvasRenderer(canvas, () => {
  const frame = currentFrame();
  return {
    snapshot: frame?.snapshot ?? null,
    options: { selfGuid: props.selfGuid, timeMs: performance.now(), damage: props.damage, origin: frame?.camera?.origin, view: frame?.camera?.view },
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
