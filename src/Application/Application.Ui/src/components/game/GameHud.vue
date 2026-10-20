<script setup lang="ts">
import ProfileButton from "@/components/account/ProfileButton.vue";
import { computed } from "vue";
import { useConfirmClick } from "@/composables/useConfirmClick";
import { getText } from "@/helpers";
import type { GameConnectionStatus, LeaderboardEntry } from "@/types/game";

const props = defineProps<{
  nickname: string;
  score: number;
  /** Round trip to the game server in ms, or null before it is known. */
  ping: number | null;
  leaderboard: LeaderboardEntry[];
  status: GameConnectionStatus;
  mouseAim: boolean;
  /** Controls and settings are shown only when asked for, so the screen stays clear. */
  details: boolean;
}>();

const emit = defineEmits<{
  (event: "toggle-mouse-aim"): void;
  (event: "toggle-details"): void;
  (event: "leave"): void;
}>();

/** Leaving ends the snake, so it takes two clicks. */
const leaving = useConfirmClick();

const connectionNotice = computed(() => {
  if (props.status === "reconnecting") return getText({ key: "game.reconnecting" });
  if (props.status === "closed" || props.status === "error") return getText({ key: "game.disconnected" });
  return "";
});

/** Green while it feels instant, amber while it is noticeable, red when it hurts. */
const pingClass = computed(() => {
  if (props.ping === null) return "";
  if (props.ping < 80) return "ping-good";
  return props.ping < 150 ? "ping-fair" : "ping-bad";
});

const controls = computed(() => [
  getText({ key: "game.controls.move" }),
  getText({ key: "game.controls.shoot" }),
  getText({ key: "game.controls.boost" }),
]);
</script>

<template>
  <!-- Top left: the player panel, and under it whatever the screen adds (the invite of a private room), so they never overlap. -->
  <div class="hud-left">
    <section class="hud-panel hud-player">
      <ProfileButton variant="icon" class="hud-profile" />
      <div class="hud-name">{{ nickname }}</div>
      <div class="hud-score">{{ getText({ key: "game.score" }) }}: {{ score }}</div>
      <div v-if="ping !== null" class="hud-ping" :class="pingClass">{{ getText({ key: "game.ping" }) }} {{ ping }} ms</div>
      <div v-if="connectionNotice" class="hud-notice">{{ connectionNotice }}</div>
    </section>
    <slot name="below-player" />
  </div>

  <!-- Top right: the ranking, and under it whatever the screen adds (an unlocked achievement), so they never overlap. -->
  <div class="hud-right">
    <section class="hud-panel hud-ranking">
      <div class="hud-title">{{ getText({ key: "game.leaderboard" }) }}</div>
      <ol class="hud-list">
        <li v-for="entry in leaderboard" :key="entry.nickname" class="hud-row">
          <span>{{ entry.nickname }}</span>
          <span>{{ entry.currentScore }}</span>
        </li>
      </ol>
    </section>
    <slot name="below-ranking" />
  </div>

  <button
    type="button"
    class="hud-details-toggle"
    :aria-pressed="details"
    :aria-label="getText({ key: 'game.details' })"
    :title="getText({ key: 'game.details' })"
    @click="emit('toggle-details')"
  >
    ?
  </button>

  <button
    type="button"
    class="hud-leave"
    :class="{ 'is-armed': leaving.armed.value }"
    data-test="leave"
    @click="leaving.click(() => emit('leave'))"
  >
    {{ getText({ key: leaving.armed.value ? "game.leaveConfirm" : "game.leave" }) }}
  </button>

  <section v-if="details" class="hud-panel hud-controls">
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
.hud-left {
  position: fixed;
  z-index: 10;
  top: 8px;
  left: 8px;
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 8px;
}
.hud-left .hud-panel {
  position: static;
}
.hud-player {
  position: relative !important;
  padding-right: 44px;
}
.hud-profile {
  position: absolute;
  top: 6px;
  right: 6px;
}
.hud-right {
  position: fixed;
  z-index: 10;
  top: 8px;
  right: 8px;
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 8px;
}
.hud-right .hud-panel {
  position: static;
}
.hud-ranking {
  min-width: 160px;
}
.hud-leave {
  position: fixed;
  z-index: 11;
  bottom: 8px;
  left: 48px;
  height: 32px;
  padding: 0 12px;
  color: #ffffff;
  font-family: monospace;
  font-size: 12px;
  background: rgba(11, 13, 23, 0.78);
  border: 2px solid var(--snake-line);
  cursor: pointer;
  opacity: 0.7;
}
.hud-leave:hover,
.hud-leave:focus-visible {
  opacity: 1;
  border-color: #ff5a5a;
}
.hud-leave.is-armed {
  opacity: 1;
  color: #ffffff;
  background: #b3262e;
  border-color: #ff5a5a;
}
.hud-details-toggle {
  position: fixed;
  z-index: 11;
  bottom: 8px;
  left: 8px;
  width: 32px;
  height: 32px;
  color: #ffffff;
  font-family: monospace;
  font-size: 16px;
  font-weight: bold;
  background: rgba(11, 13, 23, 0.78);
  border: 2px solid var(--snake-line);
  cursor: pointer;
  opacity: 0.7;
}
.hud-details-toggle[aria-pressed="true"],
.hud-details-toggle:hover,
.hud-details-toggle:focus-visible {
  opacity: 1;
  border-color: var(--snake-green);
}
.hud-controls {
  bottom: 48px;
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
.hud-ping {
  margin-top: 2px;
  font-size: 12px;
}
.ping-good {
  color: var(--snake-green);
}
.ping-fair {
  color: var(--snake-amber);
}
.ping-bad {
  color: #ff5a5a;
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
