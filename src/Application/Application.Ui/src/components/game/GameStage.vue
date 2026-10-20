<script setup lang="ts">
import { useElementSize } from "@vueuse/core";
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import PerfOverlay from "@/components/game/PerfOverlay.vue";
import { onDetectTiming } from "@/input/gaze/detectTimings";
import { FramePerf } from "@/render/framePerf";
import { useCanvasRenderer } from "@/composables/useCanvasRenderer";
import { followCamera, viewLayout, type Camera } from "@/render/camera";
import { StrikeTracker } from "@/render/strikes";
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

/** How smooth the game is: also at `window.__snakePerf`, so it can be read from the console. */
const perf = new FramePerf();
const showPerf = ref(new URLSearchParams(window.location.search).has("perf"));
function togglePerf(event: KeyboardEvent) {
  if (event.key === "F3") {
    event.preventDefault();
    showPerf.value = !showPerf.value;
  }
}
let stopDetectTiming: (() => void) | null = null;
let longTaskObserver: PerformanceObserver | null = null;
let heapTimer = 0;
onMounted(() => {
  (window as unknown as { __snakePerf?: FramePerf }).__snakePerf = perf;
  window.addEventListener("keydown", togglePerf);
  stopDetectTiming = onDetectTiming((at, ms) => perf.detect(at, ms));
  // What blocked the page, if the browser says: long tasks, and the memory in use (a fall of it is a garbage collection).
  try {
    longTaskObserver = new PerformanceObserver((list) => list.getEntries().forEach((entry) => perf.longTask(entry.startTime + entry.duration, entry.duration)));
    longTaskObserver.observe({ entryTypes: ["longtask"] });
  } catch {
    longTaskObserver = null;
  }
  const memory = (performance as unknown as { memory?: { usedJSHeapSize: number } }).memory;
  if (memory) heapTimer = window.setInterval(() => perf.heap(performance.now(), memory.usedJSHeapSize), 250);
});
onBeforeUnmount(() => {
  window.removeEventListener("keydown", togglePerf);
  stopDetectTiming?.();
  longTaskObserver?.disconnect();
  window.clearInterval(heapTimer);
});
/** Lightning warnings are shown as they arrive, not a moment in the past: the countdown is the server's, not the snakes' motion. */
const strikeTracker = new StrikeTracker();
watch(
  () => props.snapshot,
  (snapshot) => {
    if (snapshot) {
      buffer.push(snapshot, performance.now());
      perf.snapshot(
        performance.now(),
        snapshot.snakes.reduce((total, snake) => total + snake.path.length, 0)
      );
      strikeTracker.observeFrame(snapshot, performance.now());
    } else {
      buffer.clear();
      strikeTracker.clear();
    }
  },
  { immediate: true }
);

interface Frame {
  snapshot: GameSnapshot;
  camera: Camera | null;
}

/** The last frame worked out, and when: the aim sources ask where the camera is far more often than a frame is drawn. */
let worked: { frame: Frame | null; at: number } | null = null;
/** Something that only asks where the camera is may use a frame this old. */
const REUSE_FRAME_MS = 8;

/** What to draw now, and where the camera is. Null until there is a snapshot. */
function workOutFrame(): Frame | null {
  const now = performance.now();
  perf.sampled(now);
  const snapshot = buffer.sample(now);
  if (!snapshot) return null;
  if (!layout.value) return { snapshot, camera: null };
  const focus = target(snapshot);
  const center = smoother.step(focus, now, snapshot.room);
  const camera = followCamera(layout.value, snapshot.room, focus, center);
  perf.camera(camera.origin, now);
  return { snapshot, camera };
}

/** A frame for drawing is always fresh; a question about the camera is answered from the last frame when that is recent. */
function currentFrame(fresh = true): Frame | null {
  const now = performance.now();
  if (!fresh && worked && now - worked.at < REUSE_FRAME_MS) return worked.frame;
  worked = { frame: workOutFrame(), at: now };
  return worked.frame;
}

const currentCamera = (): Camera | null => currentFrame(false)?.camera ?? null;

/** Where the canvas is on the page, so pointer positions can be turned into positions on the screen. */
function getRect(): StageRect | null {
  const box = canvas.value?.getBoundingClientRect();
  return box ? { left: box.left, top: box.top, width: box.width, height: box.height } : null;
}

defineExpose({ getRect, getCamera: currentCamera });

useCanvasRenderer(
  canvas,
  () => {
  const frame = currentFrame();
  return {
    snapshot: frame?.snapshot ?? null,
    options: {
      selfGuid: props.selfGuid,
      timeMs: performance.now(),
      damage: props.damage,
      strikes: strikeTracker.view(performance.now()),
      origin: frame?.camera?.origin,
      view: frame?.camera?.view,
    },
  };
  },
  (now, drawMs) => perf.frame(now, drawMs, buffer.starved)
);
</script>

<template>
  <canvas ref="canvas" class="game-stage" />
  <PerfOverlay v-if="showPerf" :perf="perf" />
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
