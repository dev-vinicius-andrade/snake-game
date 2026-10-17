<script setup lang="ts">
import { computed } from "vue";
import { getText } from "@/helpers";
import type { CalibrationTarget } from "@/input/gaze/calibration";
import type { TrackingState } from "@/input/gaze/trackingMonitor";

const props = defineProps<{
  target: CalibrationTarget | null;
  progress: number;
  failed: boolean;
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
  return getText({ key: props.failed ? "game.eye.calibrationFailed" : "game.eye.lookAtDot" });
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
  width: 28px;
  height: 28px;
  margin: -14px 0 0 -14px;
  background: #ffcc00;
  border: 3px solid #ffffff;
  border-radius: 50%;
  box-shadow: 0 0 18px rgba(255, 204, 0, 0.9);
}
.calibration-box {
  position: absolute;
  bottom: 10%;
  left: 50%;
  width: min(420px, 90vw);
  padding: 12px 16px;
  color: #ffffff;
  text-align: center;
  background: #2b2b36;
  border-radius: 4px;
  transform: translateX(-50%);
}
.calibration-message {
  margin-bottom: 8px;
}
.calibration-warning {
  color: #ffcc00;
}
.calibration-bar {
  height: 6px;
  margin-bottom: 10px;
  background: rgba(255, 255, 255, 0.2);
}
.calibration-fill {
  height: 100%;
  background: #4caf50;
}
.calibration-cancel {
  padding: 2px 10px;
  color: inherit;
  font: inherit;
  background: transparent;
  border: 1px solid rgba(255, 255, 255, 0.5);
  border-radius: 2px;
  cursor: pointer;
}
</style>
