<script setup lang="ts">
import { computed } from "vue";
import type { EyeControlPhase } from "@/composables/useEyeControl";
import { getText } from "@/helpers";
import type { CameraDevice } from "@/input/gaze/cameraDevices";
import type { CameraErrorKind } from "@/input/gaze/cameraErrors";
import type { FaceMode } from "@/input/gaze/mediapipeGazeSource";
import type { TrackingState } from "@/input/gaze/trackingMonitor";

const props = defineProps<{
  phase: EyeControlPhase;
  mode: FaceMode;
  tracking: TrackingState;
  errorKind: CameraErrorKind | null;
  cameras: CameraDevice[];
  activeCameraId: string | null;
  virtualCameraWarning: boolean;
}>();

const emit = defineEmits<{
  (event: "enable"): void;
  (event: "disable"): void;
  (event: "recalibrate"): void;
  (event: "select-camera", cameraId: string): void;
  (event: "set-mode", mode: FaceMode): void;
}>();

const isOn = computed(() => props.phase === "starting" || props.phase === "calibrating" || props.phase === "active");

const ERROR_KEYS: Record<CameraErrorKind, string> = {
  denied: "game.eye.errors.denied",
  "no-camera": "game.eye.errors.noCamera",
  busy: "game.eye.errors.busy",
  unsupported: "game.eye.errors.unsupported",
  model: "game.eye.errors.model",
  unknown: "game.eye.errors.unknown",
};

const status = computed(() => {
  if (props.phase === "starting") return getText({ key: "game.eye.starting" });
  if (props.phase === "error") return getText({ key: ERROR_KEYS[props.errorKind ?? "unknown"] });
  if (props.phase === "active" && props.tracking === "lost") return getText({ key: "game.eye.faceLost" });
  return "";
});

function onCameraChange(event: Event) {
  emit("select-camera", (event.target as HTMLSelectElement).value);
}

function onModeChange(event: Event) {
  emit("set-mode", (event.target as HTMLSelectElement).value as FaceMode);
}

function toggle() {
  if (isOn.value) emit("disable");
  else emit("enable");
}
</script>

<template>
  <section class="eye-panel">
    <button type="button" class="eye-button" :aria-pressed="isOn" @click="toggle">
      {{ getText({ key: isOn ? "game.eye.on" : "game.eye.off" }) }}
    </button>
    <button v-if="phase === 'active'" type="button" class="eye-button" @click="emit('recalibrate')">
      {{ getText({ key: "game.eye.recalibrate" }) }}
    </button>
    <label class="eye-camera">
      <span>{{ getText({ key: "game.eye.mode" }) }}</span>
      <select class="eye-select" :value="mode" @change="onModeChange">
        <option value="head">{{ getText({ key: "game.eye.modeHead" }) }}</option>
        <option value="eyes">{{ getText({ key: "game.eye.modeEyes" }) }}</option>
      </select>
    </label>
    <div v-if="phase === 'active'" class="eye-hint">{{ getText({ key: mode === "head" ? "game.eye.headHint" : "game.eye.eyesHint" }) }}</div>
    <label v-if="cameras.length > 0" class="eye-camera">
      <span>{{ getText({ key: "game.eye.camera" }) }}</span>
      <select class="eye-select" :value="activeCameraId ?? ''" @change="onCameraChange">
        <option v-for="camera in cameras" :key="camera.id" :value="camera.id">{{ camera.label }}</option>
      </select>
    </label>
    <div v-if="virtualCameraWarning" class="eye-status eye-status-error">{{ getText({ key: "game.eye.virtualCamera" }) }}</div>
    <div v-if="status" class="eye-status" :class="{ 'eye-status-error': phase === 'error' || tracking === 'lost' }" role="status">
      {{ status }}
    </div>
    <div v-if="!isOn" class="eye-privacy">{{ getText({ key: "game.eye.privacy" }) }}</div>
  </section>
</template>

<style scoped>
.eye-panel {
  position: fixed;
  right: 8px;
  bottom: 8px;
  z-index: 10;
  max-width: 260px;
  padding: 8px 12px;
  color: #ffffff;
  font-family: monospace;
  font-size: 12px;
  background: rgba(0, 0, 0, 0.45);
}
.eye-button {
  margin-right: 6px;
  padding: 2px 8px;
  color: inherit;
  font: inherit;
  background: rgba(255, 255, 255, 0.15);
  border: 1px solid rgba(255, 255, 255, 0.4);
  border-radius: 2px;
  cursor: pointer;
}
.eye-camera {
  display: flex;
  flex-direction: column;
  gap: 2px;
  margin-top: 6px;
}
.eye-select {
  max-width: 100%;
  padding: 2px 4px;
  color: #ffffff;
  font: inherit;
  background: #2b2b36;
  border: 1px solid rgba(255, 255, 255, 0.4);
  border-radius: 2px;
}
.eye-status {
  margin-top: 6px;
}
.eye-status-error {
  color: #ffcc00;
}
.eye-hint {
  margin-top: 6px;
  opacity: 0.85;
}
.eye-privacy {
  margin-top: 6px;
  opacity: 0.7;
}
</style>
