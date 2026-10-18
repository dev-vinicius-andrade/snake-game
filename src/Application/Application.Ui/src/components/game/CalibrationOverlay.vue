<script setup lang="ts">
import { computed } from "vue";
import { getText } from "@/helpers";
import type { CalibrationTarget } from "@/input/gaze/calibration";
import type { FaceMode } from "@/input/gaze/mediapipeGazeSource";
import type { TrackingState } from "@/input/gaze/trackingMonitor";

const props = defineProps<{
  target: CalibrationTarget | null;
  progress: number;
  failed: boolean;
  mode?: FaceMode;
  tracking: TrackingState;
}>();

const emit = defineEmits<{
  (event: "cancel"): void;
}>();

const dotStyle = computed(() => ({
  left: `${(props.target?.x ?? 0.5) * 100}%`,
  top: `${(props.target?.y ?? 0.5) * 100}%`,
}));

const message = computed(() => {
  if (props.tracking === "lost") return getText({ key: "game.eye.faceLost" });
  if (props.failed) return getText({ key: props.mode === "head" ? "game.eye.headCalibrationFailed" : "game.eye.calibrationFailed" });
  return getText({ key: props.mode === "head" ? "game.eye.turnToDot" : "game.eye.lookAtDot" });
});
</script>

<template>
  <div v-if="target" class="calibration" role="dialog" aria-live="polite">
    <div class="calibration-dot" :style="dotStyle" />
    <div class="calibration-box">
      <div class="calibration-message" :class="{ 'calibration-warning': failed || tracking === 'lost' }">{{ message }}</div>
      <div class="calibration-bar" role="progressbar" :aria-valuenow="Math.round(progress * 100)" aria-valuemin="0" aria-valuemax="100">
        <div class="calibration-fill" :style="{ width: `${progress * 100}%` }" />
      </div>
      <button type="button" class="calibration-cancel" @click="emit('cancel')">{{ getText({ key: "game.eye.cancel" }) }}</button>
    </div>
  </div>
</template>

<style scoped>
.calibration {
  position: fixed;
  inset: 0;
  z-index: 30;
  background: rgba(0, 0, 0, 0.7);
}
.calibration-dot {
  position: absolute;
  /* Above the message box, so a target is never hidden, even on a short window. */
  z-index: 2;
  width: 28px;
  height: 28px;
  margin: -14px 0 0 -14px;
  background: var(--snake-amber);
  border: 3px solid #ffffff;
  box-shadow: 0 0 0 4px rgba(0, 0, 0, 0.6);
}
.calibration-box {
  position: absolute;
  /* Between the top dot (12%) and the centre dot (50%): none of the five targets is ever under the box. */
  top: 26%;
  z-index: 1;
  left: 50%;
  width: min(420px, 90vw);
  padding: 12px 16px;
  color: var(--snake-text);
  text-align: center;
  background: var(--snake-surface);
  border: 3px solid var(--snake-line);
  transform: translateX(-50%);
}
.calibration-message {
  margin-bottom: 8px;
}
.calibration-warning {
  color: var(--snake-amber);
}
.calibration-bar {
  height: 6px;
  margin-bottom: 10px;
  background: rgba(255, 255, 255, 0.2);
}
.calibration-fill {
  height: 100%;
  background: var(--snake-green);
}
.calibration-cancel {
  padding: 2px 10px;
  color: inherit;
  font: inherit;
  background: transparent;
  border: 2px solid var(--snake-line);
  cursor: pointer;
}
</style>
