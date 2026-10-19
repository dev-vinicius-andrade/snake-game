<script setup lang="ts">
import { computed } from "vue";
import { GAME_ICON_SIZE, iconSource } from "@/game/gameIcons";

/**
 * An icon by its name: a game icon (`game-icons:magnet`, drawn from the few bodies that ship) or a Material Design icon (`mdi-skull`).
 * Takes the colour of the text around it and the size of the text, or `size` in pixels.
 */
const props = defineProps<{ name: string | null | undefined; size?: number }>();

const source = computed(() => iconSource(props.name));
const pixels = computed(() => (props.size ? `${props.size}px` : "1.25em"));
</script>

<template>
  <svg
    v-if="source.kind === 'game'"
    class="game-icon"
    :width="pixels"
    :height="pixels"
    :viewBox="`0 0 ${GAME_ICON_SIZE} ${GAME_ICON_SIZE}`"
    fill="currentColor"
    aria-hidden="true"
    focusable="false"
    v-html="source.body"
  />
  <VIcon v-else :size="size" aria-hidden="true">{{ source.name }}</VIcon>
</template>

<style scoped>
.game-icon {
  display: inline-block;
  vertical-align: -0.2em;
  flex: none;
}
</style>
