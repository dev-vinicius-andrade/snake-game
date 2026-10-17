<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted } from "vue";
import { useRouter } from "vue-router";
import { useGameConnection } from "@/composables/useGameConnection";
import { useManagerJoin } from "@/composables/useManagerJoin";
import { useSnakeSession } from "@/composables/useSnakeSession";
import { usePlayerStore } from "@/store/usePlayerStore";

const router = useRouter();
const playerStore = usePlayerStore();
const game = useGameConnection();
const session = useSnakeSession({ joiner: useManagerJoin(), game });

const nickname = computed(() => playerStore.data?.nickname?.trim() ?? "");

function play() {
  return session.start(nickname.value);
}

async function leave() {
  await session.stop();
  await router.replace({ name: "home" });
}

onMounted(() => (nickname.value ? play() : router.replace({ name: "home" })));
onBeforeUnmount(() => void session.stop());
</script>

<template>
  <div class="snake-view">
    <GameStage :snapshot="game.snapshot.value" :self-guid="game.self.value?.guid" />
    <GameHud
      :nickname="nickname"
      :score="game.score.value"
      :leaderboard="game.leaderboard.value"
      :status="game.status.value"
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
