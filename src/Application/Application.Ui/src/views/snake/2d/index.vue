<script setup lang="ts">
import { useStorage } from "@vueuse/core";
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import GameStage from "@/components/game/GameStage.vue";
import { useEyeControl } from "@/composables/useEyeControl";
import { useGameConnection } from "@/composables/useGameConnection";
import { useGazeControls } from "@/composables/useGazeControls";
import { useManagerJoin } from "@/composables/useManagerJoin";
import { useSnakeSession } from "@/composables/useSnakeSession";
import { createCalibrationStorage } from "@/input/gaze/calibrationStorage";
import { createCameraPreferenceStorage } from "@/input/gaze/cameraPreference";
import { createMediaPipeGazeSource } from "@/input/gaze/mediapipeGazeSource";
import { createPointerSource } from "@/input/gaze/pointerSource";
import { usePlayerStore } from "@/store/usePlayerStore";

const router = useRouter();
const playerStore = usePlayerStore();
const game = useGameConnection();
const session = useSnakeSession({ joiner: useManagerJoin(), game });

const stage = ref<InstanceType<typeof GameStage> | null>(null);
const mouseAim = useStorage("snake.mouseAim", false);
const aim = useGazeControls({
  source: createPointerSource({ target: window, getRect: () => stage.value?.getRect() ?? null }),
  getHead: () => game.self.value?.head ?? null,
  getRoom: () => game.room.value,
  onIntent: session.handleIntent,
  canSteer: session.allowsGazeSteer,
  enabled: () => mouseAim.value && session.phase.value === "playing",
});

const eye = useEyeControl({
  source: createMediaPipeGazeSource(),
  storage: createCalibrationStorage(),
  cameraStorage: createCameraPreferenceStorage(),
  getRoom: () => game.room.value,
  getHead: () => game.self.value?.head ?? null,
  onIntent: session.handleIntent,
  canSteer: session.allowsGazeSteer,
});

/** Mouse aim and eye control both steer, so only one of them can be on. */
function toggleMouseAim() {
  mouseAim.value = !mouseAim.value;
  if (mouseAim.value) eye.disable();
}

function enableEyeControl() {
  mouseAim.value = false;
  return eye.enable();
}

const nickname = computed(() => playerStore.data?.nickname?.trim() ?? "");

function play() {
  return session.start(nickname.value);
}

async function leave() {
  await session.stop();
  await router.replace({ name: "home" });
}

onMounted(() => {
  void aim.start();
  return nickname.value ? play() : router.replace({ name: "home" });
});
onBeforeUnmount(() => void session.stop());
</script>

<template>
  <div class="snake-view">
    <GameStage ref="stage" :snapshot="game.snapshot.value" :self-guid="game.self.value?.guid" />
    <GameHud
      :nickname="nickname"
      :score="game.score.value"
      :leaderboard="game.leaderboard.value"
      :status="game.status.value"
      :mouse-aim="mouseAim"
      @toggle-mouse-aim="toggleMouseAim"
    />
    <EyeControlPanel
      :phase="eye.phase.value"
      :tracking="eye.tracking.value"
      :error-kind="eye.errorKind.value"
      :cameras="eye.cameras.value"
      :active-camera-id="eye.activeCamera.value?.id ?? null"
      :virtual-camera-warning="eye.virtualCameraWarning.value"
      @enable="enableEyeControl"
      @disable="eye.disable"
      @recalibrate="eye.recalibrate"
      @select-camera="eye.selectCamera"
    />
    <CalibrationOverlay
      v-if="eye.phase.value === 'calibrating'"
      :target="eye.calibrationTarget.value"
      :progress="eye.calibrationProgress.value"
      :failed="eye.calibrationFailed.value"
      :tracking="eye.tracking.value"
      @cancel="eye.disable"
    />
    <SessionOverlay
      :phase="session.phase.value"
      :score="game.death.value?.score ?? 0"
      :error="session.error.value"
      @play-again="play"
      @leave="leave"
    />
  </div>
</template>

<style scoped>
.snake-view {
  width: 100%;
  min-height: 100vh;
  background: #16161d;
}
</style>
