<script setup lang="ts">
import { getText } from "@/helpers";
import type { TimedEvent } from "@/render/timedEvents";

/** The banners of the events that are only a countdown for the player (a fruit shower, a sugar rush), one under the other. */
defineProps<{ events: TimedEvent[] }>();

const ICONS: Record<TimedEvent["kind"], string> = { shower: "✿", rush: "⇶" };
const seconds = (event: TimedEvent) => Math.ceil(event.msLeft / 1000);
</script>

<template>
  <div v-if="events.length > 0" class="timed-hints" role="status" aria-live="polite">
    <div v-for="event in events" :key="event.kind" class="timed-hint" :class="`timed-${event.kind}`">
      <span aria-hidden="true">{{ ICONS[event.kind] }}</span>
      <span>{{ getText({ key: `game.${event.kind}`, params: { seconds: seconds(event) } }) }}</span>
    </div>
  </div>
</template>

<style scoped>
.timed-hints {
  position: fixed;
  z-index: 10;
  top: 198px;
  left: 50%;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 8px;
  transform: translateX(-50%);
  pointer-events: none;
}
.timed-hint {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 4px 10px;
  font-family: monospace;
  font-size: 14px;
  background: rgba(11, 13, 23, 0.78);
  border: 2px solid;
}
.timed-shower {
  color: #7dff6a;
  border-color: #2f7a24;
}
.timed-rush {
  color: #ff7ad9;
  border-color: #8a2f74;
}
</style>
