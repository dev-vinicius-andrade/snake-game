<script setup lang="ts">
import { useMediaQuery } from "@vueuse/core";
import { computed, ref } from "vue";
import { getText } from "@/helpers";
import type { ControlIntent } from "@/input/intents";
import { endStick, moveStick, startStick, type StickState } from "@/input/touchStick";

const emit = defineEmits<{ (event: "intent", intent: ControlIntent): void }>();

/** Shown on phones and tablets; a mouse or keyboard has its own controls. */
const isTouch = useMediaQuery("(pointer: coarse)");

const stick = ref<StickState>(endStick());
const knob = ref({ x: 0, y: 0 });
const stickPointer = ref<number | null>(null);

const ringStyle = computed(() => ({
  left: `${stick.value.origin?.x ?? 0}px`,
  top: `${stick.value.origin?.y ?? 0}px`,
}));
const knobStyle = computed(() => ({ transform: `translate(${knob.value.x}px, ${knob.value.y}px)` }));

function stickDown(event: PointerEvent) {
  if (stickPointer.value !== null) return;
  stickPointer.value = event.pointerId;
  (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);
  stick.value = startStick({ x: event.clientX, y: event.clientY });
  knob.value = { x: 0, y: 0 };
}

function stickMove(event: PointerEvent) {
  if (event.pointerId !== stickPointer.value) return;
  const moved = moveStick(stick.value, { x: event.clientX, y: event.clientY });
  stick.value = moved.state;
  knob.value = moved.knob;
  if (moved.intent) emit("intent", moved.intent);
}

function stickUp(event: PointerEvent) {
  if (event.pointerId !== stickPointer.value) return;
  stickPointer.value = null;
  stick.value = endStick();
  knob.value = { x: 0, y: 0 };
}

function shootDown() {
  emit("intent", { type: "shoot" });
}

function shootUp() {
  emit("intent", { type: "shootRelease" });
}

function boosting(active: boolean) {
  emit("intent", { type: "boost", active });
}

</script>

<template>
  <div v-if="isTouch" class="touch-controls">
    <div
      class="stick-zone"
      @pointerdown.prevent="stickDown"
      @pointermove.prevent="stickMove"
      @pointerup.prevent="stickUp"
      @pointercancel.prevent="stickUp"
    />
    <div v-if="stick.origin" class="stick-ring" :style="ringStyle">
      <div class="stick-knob" :style="knobStyle" />
    </div>
    <div class="touch-buttons">
      <button
        type="button"
        class="touch-button"
        @pointerdown.prevent="shootDown"
        @pointerup.prevent="shootUp"
        @pointercancel.prevent="shootUp"
        @contextmenu.prevent
      >
        {{ getText({ key: "game.touch.shoot" }) }}
      </button>
      <button
        type="button"
        class="touch-button"
        @pointerdown.prevent="boosting(true)"
        @pointerup.prevent="boosting(false)"
        @pointercancel.prevent="boosting(false)"
        @contextmenu.prevent
      >
        {{ getText({ key: "game.touch.boost" }) }}
      </button>
    </div>
  </div>
</template>

<style scoped>
.touch-controls {
  position: fixed;
  inset: 0;
  z-index: 9;
  pointer-events: none;
  touch-action: none;
  user-select: none;
  -webkit-user-select: none;
}
.stick-zone {
  position: absolute;
  top: 20%;
  bottom: 0;
  left: 0;
  width: 58%;
  pointer-events: auto;
  touch-action: none;
}
.stick-ring {
  position: fixed;
  width: 96px;
  height: 96px;
  margin: -48px 0 0 -48px;
  border: 3px solid rgba(255, 255, 255, 0.45);
  border-radius: 50%;
  pointer-events: none;
}
.stick-knob {
  position: absolute;
  top: 28px;
  left: 28px;
  width: 40px;
  height: 40px;
  background: rgba(255, 255, 255, 0.55);
  border-radius: 50%;
}
.touch-buttons {
  position: absolute;
  right: 12px;
  bottom: 16px;
  display: flex;
  flex-direction: column;
  gap: 14px;
}
.touch-button {
  width: 84px;
  height: 84px;
  color: #ffffff;
  font-family: monospace;
  font-size: 14px;
  font-weight: bold;
  background: rgba(11, 13, 23, 0.7);
  border: 3px solid var(--snake-line);
  border-radius: 50%;
  pointer-events: auto;
  touch-action: none;
}
.touch-button:active {
  background: var(--snake-surface-2);
}
</style>
