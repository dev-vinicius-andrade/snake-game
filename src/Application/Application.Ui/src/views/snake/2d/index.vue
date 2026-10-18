<script setup lang="ts">
import { useScrollLock, useStorage } from "@vueuse/core";
import { computed, onBeforeUnmount, onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import InviteBadge from "@/components/rooms/InviteBadge.vue";
import type { JoinTarget } from "@/composables/useManagerJoin";
import GameStage from "@/components/game/GameStage.vue";
import TouchControls from "@/components/game/TouchControls.vue";
import PerkList from "@/components/game/PerkList.vue";
import PrizeHint from "@/components/game/PrizeHint.vue";
import { useEyeControl } from "@/composables/useEyeControl";
import { useGameConnection } from "@/composables/useGameConnection";
import { useGazeControls } from "@/composables/useGazeControls";
import { useManagerJoin } from "@/composables/useManagerJoin";
import { useSnakeSession } from "@/composables/useSnakeSession";
import { HEAD_CALIBRATION_KEY, createCalibrationStorage } from "@/input/gaze/calibrationStorage";
import { createFaceModePreference } from "@/input/gaze/faceModePreference";
import { createCameraPreferenceStorage } from "@/input/gaze/cameraPreference";
import { createMediaPipeGazeSource } from "@/input/gaze/mediapipeGazeSource";
import { createPointerSource } from "@/input/gaze/pointerSource";
import { targetOnScreen } from "@/render/camera";
import { prizeCompass } from "@/render/prizeCompass";
import { DEFAULT_HEAD_SIZE } from "@/render/pixelRenderer";
import { usePlayerStore } from "@/store/usePlayerStore";

const router = useRouter();
const route = useRoute();
const playerStore = usePlayerStore();
const game = useGameConnection({ pingIntervalMs: 2000 });
/** Own snake, read from the latest snapshot; the session asks whether the auto fire perk is on. */
const ownAutoFire = () => !!game.snapshot.value?.snakes.find((snake) => snake.guid === game.self.value?.guid)?.autoFire;
const session = useSnakeSession({ joiner: useManagerJoin(), game, autoFire: ownAutoFire });

const stage = ref<InstanceType<typeof GameStage> | null>(null);
const ownSnake = computed(() => game.snapshot.value?.snakes.find((snake) => snake.guid === game.self.value?.guid) ?? null);
const compass = computed(() => (game.snapshot.value ? prizeCompass(game.snapshot.value, game.self.value?.guid) : null));
const mouseAim = useStorage("snake.mouseAim", false);
/** Controls, mouse aim and face control settings: out of the way until asked for. */
const showDetails = useStorage("snake.hudDetails", false);

/**
 * The camera follows the own snake, so what the aim sources see as "the room" is the part of the world on screen,
 * and the head is measured from the top-left of that part. Steering only depends on the difference between the two.
 */
const visibleRoom = () => stage.value?.getCamera()?.size ?? null;
const headOnScreen = () => {
  const camera = stage.value?.getCamera();
  if (!camera || !game.self.value) return null;
  // The screen shows the world slightly in the past and the camera follows a little behind: measure from what is on screen.
  const head = targetOnScreen(camera);
  return { x: head.x - DEFAULT_HEAD_SIZE / 2, y: head.y - DEFAULT_HEAD_SIZE / 2 };
};
const aim = useGazeControls({
  source: createPointerSource({ target: window, getRect: () => stage.value?.getRect() ?? null }),
  getHead: headOnScreen,
  getRoom: visibleRoom,
  onIntent: session.handleIntent,
  canSteer: session.allowsGazeSteer,
  enabled: () => mouseAim.value && session.phase.value === "playing",
});

const faceMode = createFaceModePreference();
const eye = useEyeControl({
  source: createMediaPipeGazeSource(),
  storage: createCalibrationStorage(),
  headStorage: createCalibrationStorage(undefined, HEAD_CALIBRATION_KEY),
  modeStorage: faceMode,
  mode: faceMode.load() ?? "head",
  cameraStorage: createCameraPreferenceStorage(),
  getRoom: visibleRoom,
  getHead: headOnScreen,
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

/** A host or a friend arrives with the room id and the invite code in the address. */
const inviteCode = computed(() => (typeof route.query.code === "string" ? route.query.code : ""));
const joinTarget = computed<JoinTarget | undefined>(() => {
  if (typeof route.query.room === "string" && route.query.room) return route.query.room;
  return inviteCode.value ? { code: inviteCode.value } : undefined;
});

function play() {
  return session.start(nickname.value, joinTarget.value);
}

async function leave() {
  await session.stop();
  await router.replace({ name: "home" });
}

// The game fills the screen: the page behind it must not scroll.
const scrollLock = useScrollLock(document.body);

onMounted(() => {
  scrollLock.value = true;
  void aim.start();
  return nickname.value ? play() : router.replace({ name: "home" });
});
onBeforeUnmount(() => {
  scrollLock.value = false;
  void session.stop();
});
</script>

<template>
  <div class="snake-view">
    <GameStage ref="stage" :snapshot="game.snapshot.value" :self-guid="game.self.value?.guid" :damage="game.damage.value" />
    <GameHud
      :nickname="nickname"
      :score="game.score.value"
      :ping="game.ping.value"
      :leaderboard="game.leaderboard.value"
      :status="game.status.value"
      :mouse-aim="mouseAim"
      :details="showDetails"
      @toggle-mouse-aim="toggleMouseAim"
      @toggle-details="showDetails = !showDetails"
    />
    <PerkList :perks="ownSnake?.perks ?? []" :foods-eaten="ownSnake?.foodsEaten ?? 0" />
    <PrizeHint :compass="compass" />
    <TouchControls v-if="session.phase.value === 'playing'" @intent="session.handleIntent" />
    <InviteBadge v-if="inviteCode" :code="inviteCode" />
    <EyeControlPanel
      v-if="showDetails || eye.phase.value === 'error'"
      :phase="eye.phase.value"
      :mode="eye.mode.value"
      :tracking="eye.tracking.value"
      :error-kind="eye.errorKind.value"
      :cameras="eye.cameras.value"
      :active-camera-id="eye.activeCamera.value?.id ?? null"
      :virtual-camera-warning="eye.virtualCameraWarning.value"
      @enable="enableEyeControl"
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
  position: fixed;
  inset: 0;
  overflow: hidden;
  background: var(--snake-bg);
}
</style>
