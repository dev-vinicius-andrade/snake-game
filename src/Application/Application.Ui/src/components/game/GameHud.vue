<script setup lang="ts">
import { computed } from "vue";
import { getText } from "@/helpers";
import type { GameConnectionStatus, LeaderboardEntry } from "@/types/game";

const props = defineProps<{
  nickname: string;
  score: number;
  leaderboard: LeaderboardEntry[];
  status: GameConnectionStatus;
  mouseAim: boolean;
}>();

const emit = defineEmits<{
  (event: "toggle-mouse-aim"): void;
}>();

const connectionNotice = computed(() => {
  if (props.status === "reconnecting") return getText({ key: "game.reconnecting" });
  if (props.status === "closed" || props.status === "error") return getText({ key: "game.disconnected" });
  return "";
});

const controls = computed(() => [
  getText({ key: "game.controls.move" }),
  getText({ key: "game.controls.shoot" }),
  getText({ key: "game.controls.boost" }),
]);
</script>

<template>
  <section class="hud-panel hud-player">
    <div class="hud-name">{{ nickname }}</div>
    <div class="hud-score">{{ getText({ key: "game.score" }) }}: {{ score }}</div>
    <div v-if="connectionNotice" class="hud-notice">{{ connectionNotice }}</div>
  </section>

  <section class="hud-panel hud-ranking">
    <div class="hud-title">{{ getText({ key: "game.leaderboard" }) }}</div>
    <ol class="hud-list">
      <li v-for="entry in leaderboard" :key="entry.nickname" class="hud-row">
        <span>{{ entry.nickname }}</span>
        <span>{{ entry.currentScore }}</span>
      </li>
    </ol>
  </section>

  <section class="hud-panel hud-controls">
    <div v-for="line in controls" :key="line">{{ line }}</div>
    <button type="button" class="hud-toggle" :aria-pressed="mouseAim" @click="emit('toggle-mouse-aim')">
      {{ getText({ key: mouseAim ? "game.mouseAimOn" : "game.mouseAimOff" }) }}
    </button>
  </section>
</template>

<style scoped>
.hud-panel {
  position: fixed;
  z-index: 10;
  padding: 8px 12px;
  color: #ffffff;
  font-family: monospace;
  font-size: 14px;
  background: rgba(11, 13, 23, 0.78);
  border: 2px solid var(--snake-line);
  pointer-events: none;
}
.hud-player {
  top: 8px;
  left: 8px;
}
.hud-ranking {
  top: 8px;
  right: 8px;
  min-width: 160px;
}
.hud-controls {
  bottom: 8px;
  left: 8px;
  font-size: 12px;
  opacity: 0.8;
}
.hud-toggle {
  margin-top: 6px;
  padding: 2px 8px;
  color: inherit;
  font: inherit;
  background: var(--snake-surface-2);
  border: 2px solid var(--snake-line);
  cursor: pointer;
  pointer-events: auto;
}
.hud-name {
  font-weight: bold;
}
.hud-title {
  margin-bottom: 4px;
  font-weight: bold;
}
.hud-list {
  margin: 0;
  padding-left: 18px;
}
.hud-row {
  display: flex;
  justify-content: space-between;
  gap: 12px;
}
.hud-notice {
  margin-top: 4px;
  color: var(--snake-amber);
}

@media (max-width: 600px) {
  .hud-panel {
    padding: 6px 8px;
    font-size: 11px;
  }
  .hud-ranking {
    min-width: 0;
    max-width: 44vw;
  }
  /* A phone has no keyboard to read the keys for. */
  .hud-controls div {
    display: none;
  }
}
</style>
