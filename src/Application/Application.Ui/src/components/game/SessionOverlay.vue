<script setup lang="ts">
import { computed } from "vue";
import { getText } from "@/helpers";
import type { SessionPhase } from "@/composables/useSnakeSession";

const props = defineProps<{
  phase: SessionPhase;
  score: number;
  error: string | null;
}>();

const emit = defineEmits<{
  (event: "play-again"): void;
  (event: "leave"): void;
}>();

const visible = computed(() => props.phase === "joining" || props.phase === "dead" || props.phase === "error");
const title = computed(() => {
  if (props.phase === "joining") return getText({ key: "game.joining" });
  if (props.phase === "dead") return getText({ key: "game.died" });
  return getText({ key: "game.joinFailed" });
});
</script>

<template>
  <div v-if="visible" class="overlay">
    <div class="overlay-card">
      <h2 class="overlay-title">{{ title }}</h2>
      <p v-if="phase === 'dead'" class="overlay-text">
        {{ getText({ key: "game.finalScore", params: { score } }) }}
      </p>
      <p v-if="phase === 'error' && error" class="overlay-text">{{ error }}</p>
      <div v-if="phase !== 'joining'" class="overlay-actions">
        <VBtn color="primary" @click="emit('play-again')">
          {{ getText({ key: phase === "dead" ? "game.playAgain" : "game.retry" }) }}
        </VBtn>
        <VBtn variant="text" @click="emit('leave')">{{ getText({ key: "game.back" }) }}</VBtn>
      </div>
    </div>
  </div>
</template>

<style scoped>
.overlay {
  position: fixed;
  inset: 0;
  z-index: 20;
  display: flex;
  align-items: center;
  justify-content: center;
  background: rgba(5, 6, 13, 0.78);
}
.overlay-card {
  box-sizing: border-box;
  width: min(420px, 92vw);
  padding: 24px;
  color: var(--snake-text);
  text-align: center;
  background: var(--snake-surface);
  border: 3px solid var(--snake-line);
  box-shadow: 4px 4px 0 rgba(0, 0, 0, 0.55);
}
.overlay-title {
  margin: 0 0 16px;
  font-family: var(--snake-pixel-font);
  font-size: 1rem;
  line-height: 1.5;
}
.overlay-text {
  margin: 0 0 16px;
}
.overlay-actions {
  display: flex;
  flex-wrap: wrap;
  justify-content: center;
  gap: 12px;
}
</style>
