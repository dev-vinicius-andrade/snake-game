<script setup lang="ts">
import { computed, onMounted } from "vue";
import GameIcon from "@/components/game/GameIcon.vue";
import { useLocale } from "@/composables/useLocale";
import { useProfile } from "@/composables/useProfile";
import { achievementText } from "@/game/achievements";
import { ACHIEVEMENT_TEXTS } from "@/game/achievementTexts";
import { getText } from "@/helpers";

/** The achievement that was just unlocked, or null for none. One at a time: the queue is the caller's. */
const props = defineProps<{ id: string | null }>();

const { locale } = useLocale();
const catalog = useProfile();
// The list is cached for the page, so asking again is free; a failure just leaves the default icon.
onMounted(() => void catalog.loadDefinitions().catch(() => undefined));

const icon = computed(() => catalog.definitions.value.find((definition) => definition.id === props.id)?.icon ?? "game-icons:trophy");
const text = computed(() => (props.id ? achievementText(ACHIEVEMENT_TEXTS, locale.value, props.id) : null));
</script>

<template>
  <Transition name="achievement-toast">
    <div v-if="id && text" :key="id" class="achievement-toast" role="status" aria-live="polite" data-test="achievement-toast">
      <GameIcon :name="icon" :size="32" />
      <div class="texts">
        <div class="heading">{{ getText({ key: "profile.unlockedToast" }) }}</div>
        <div class="name">{{ text.name }}</div>
      </div>
    </div>
  </Transition>
</template>

<style scoped>
.achievement-toast {
  position: fixed;
  z-index: 20;
  top: 16px;
  right: 16px;
  display: flex;
  align-items: center;
  gap: 12px;
  max-width: min(320px, calc(100vw - 32px));
  padding: 8px 14px;
  color: #ffe14d;
  font-family: monospace;
  background: rgba(11, 13, 23, 0.88);
  border: 2px solid #8a7a1f;
  pointer-events: none;
}
.heading {
  font-size: 11px;
  text-transform: uppercase;
  opacity: 0.8;
}
.name {
  color: #ffffff;
  font-size: 15px;
}
.achievement-toast-enter-active,
.achievement-toast-leave-active {
  transition: opacity 0.2s, transform 0.2s;
}
.achievement-toast-enter-from,
.achievement-toast-leave-to {
  opacity: 0;
  transform: translateY(-8px);
}
@media (prefers-reduced-motion: reduce) {
  .achievement-toast-enter-active,
  .achievement-toast-leave-active {
    transition: none;
  }
}
</style>
