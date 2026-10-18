<script setup lang="ts">
import { ref, watch } from "vue";
import { useEyeControl } from "@/composables/useEyeControl";
import { getText } from "@/helpers";
import { HEAD_CALIBRATION_KEY, createCalibrationStorage } from "@/input/gaze/calibrationStorage";
import { createFaceModePreference } from "@/input/gaze/faceModePreference";
import { createCameraPreferenceStorage } from "@/input/gaze/cameraPreference";
import { createMediaPipeGazeSource } from "@/input/gaze/mediapipeGazeSource";

/**
 * Eye control setup on the start page: pick the camera and calibrate while nothing is moving. The calibration is
 * saved, so the game starts with eye control ready. The camera is switched off again when the page is left.
 */
const storage = createCalibrationStorage();
const headStorage = createCalibrationStorage(undefined, HEAD_CALIBRATION_KEY);
const faceMode = createFaceModePreference();
const eye = useEyeControl({
  source: createMediaPipeGazeSource(),
  storage,
  headStorage,
  modeStorage: faceMode,
  mode: faceMode.load() ?? "head",
  cameraStorage: createCameraPreferenceStorage(),
  getRoom: () => null,
  onIntent: () => undefined,
});

const isCalibrated = () => (eye.mode.value === "head" ? headStorage : storage).load() !== null;
const calibrated = ref(isCalibrated());
watch([eye.phase, eye.mode], () => {
  calibrated.value = isCalibrated();
});
</script>

<template>
  <section class="setup-hint">
    <div>{{ getText({ key: calibrated ? "game.eye.calibrated" : "game.eye.setupHint" }) }}</div>
  </section>
  <EyeControlPanel
    :phase="eye.phase.value"
    :mode="eye.mode.value"
    :tracking="eye.tracking.value"
    :error-kind="eye.errorKind.value"
    :cameras="eye.cameras.value"
    :active-camera-id="eye.activeCamera.value?.id ?? null"
    :virtual-camera-warning="eye.virtualCameraWarning.value"
    @enable="eye.enable"
    @disable="eye.disable"
    @recalibrate="eye.recalibrate"
    @select-camera="eye.selectCamera"
    @set-mode="eye.setMode"
  />
  <CalibrationOverlay
    v-if="eye.phase.value === 'calibrating'"
    :target="eye.calibrationTarget.value"
    :progress="eye.calibrationProgress.value"
    :failed="eye.calibrationFailed.value"
    :mode="eye.mode.value"
    :tracking="eye.tracking.value"
    @cancel="eye.disable"
  />
</template>

<style scoped>
.setup-hint {
  position: fixed;
  right: 8px;
  bottom: 8px;
  z-index: 9;
  max-width: 260px;
  padding: 8px 12px;
  /* Sits above the eye panel, which grows with the camera list. */
  transform: translateY(-190px);
  color: #ffffff;
  font-family: monospace;
  font-size: 12px;
  background: var(--snake-surface);
  border: 2px solid var(--snake-line);
}

@media (max-width: 600px) {
  .setup-hint {
    position: static;
    width: 100%;
    max-width: 440px;
    box-sizing: border-box;
    margin: 0 auto;
    transform: none;
  }
}
</style>
