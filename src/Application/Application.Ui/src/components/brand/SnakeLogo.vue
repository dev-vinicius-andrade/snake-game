<script setup lang="ts">
/**
 * The site's title: Vue.JSnake, each part in the colour of the technology it stands for (Vue, JavaScript, C#),
 * with their icons above. Decorative, so hidden from screen readers.
 */
defineProps<{ compact?: boolean }>();
</script>

<template>
  <div class="logo" :class="{ compact }" aria-hidden="true">
    <div class="logo-icons">
      <VIcon class="vue">mdi-vuejs</VIcon>
      <VIcon class="js">mdi-language-javascript</VIcon>
      <VIcon class="cs">mdi-language-csharp</VIcon>
    </div>
    <div class="logo-word pixel-font">
      <span class="vue">Vue</span><span class="dot">.</span><span class="js">JS</span><span class="cs">nake</span>
    </div>
  </div>
</template>

<style scoped>
.logo {
  --vue: #42d392;
  --js: #f7df1e;
  --cs: #b573e0;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 14px;
}
.logo-icons {
  display: flex;
  gap: 12px;
  font-size: 2.2rem;
}
.logo-icons :deep(.v-icon) {
  font-size: clamp(2rem, 9vw, 2.8rem);
}
.logo-word {
  font-size: clamp(1.2rem, 6.4vw, 2.4rem);
  line-height: 1.2;
  text-align: center;
  white-space: nowrap;
  text-shadow: 3px 3px 0 rgba(0, 0, 0, 0.7);
}
.vue {
  color: var(--vue);
}
.js {
  color: var(--js);
}
.cs {
  color: var(--cs);
}
.dot {
  color: var(--snake-muted);
}
.compact .logo-icons :deep(.v-icon) {
  font-size: 1.4rem;
}
.compact .logo-word {
  font-size: 0.9rem;
  text-shadow: 2px 2px 0 rgba(0, 0, 0, 0.7);
}
</style>
