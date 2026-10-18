<script setup lang="ts">
/** The site's mark: a small pixel snake and the word SNAKE in the pixel font. Decorative, so hidden from screen readers. */
defineProps<{ compact?: boolean }>();

// 1 = body, 2 = head, 3 = eye. A snake coiling in a 9x5 grid.
const PIXELS: Array<[number, number, 1 | 2 | 3]> = [
  [0, 4, 1], [1, 4, 1], [2, 4, 1], [3, 4, 1], [4, 4, 1], [5, 4, 1], [6, 4, 1],
  [6, 3, 1], [6, 2, 1], [5, 2, 1], [4, 2, 1], [3, 2, 1], [2, 2, 1],
  [2, 1, 1], [2, 0, 1], [3, 0, 1], [4, 0, 1], [5, 0, 1], [6, 0, 1], [7, 0, 2], [7, 0, 3],
];
</script>

<template>
  <div class="logo" :class="{ compact }" aria-hidden="true">
    <svg class="logo-mark" viewBox="0 0 9 5" shape-rendering="crispEdges">
      <template v-for="([x, y, kind], index) in PIXELS" :key="index">
        <rect v-if="kind === 1" :x="x" :y="y" width="1" height="1" :fill="index % 2 ? '#22a45a' : '#4ade80'" />
        <rect v-else-if="kind === 2" :x="x" :y="y" width="1" height="1" fill="#86efac" />
        <rect v-else :x="x + 0.55" :y="y + 0.2" width="0.25" height="0.25" fill="#0b0d17" />
      </template>
      <rect x="8" y="0" width="1" height="1" fill="#fbbf24" />
    </svg>
    <div class="logo-word pixel-font">SNAKE</div>
  </div>
</template>

<style scoped>
.logo {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 16px;
}
.logo-mark {
  width: clamp(120px, 38vw, 180px);
  height: auto;
}
.logo-word {
  color: var(--snake-green);
  font-size: clamp(1.9rem, 9vw, 3rem);
  text-shadow: 4px 4px 0 #14532d, 8px 8px 0 rgba(0, 0, 0, 0.5);
}
.compact .logo-mark {
  width: 72px;
}
.compact .logo-word {
  font-size: 1.1rem;
  text-shadow: 2px 2px 0 #14532d;
}
</style>
