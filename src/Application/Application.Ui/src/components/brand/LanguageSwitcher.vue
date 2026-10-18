<script setup lang="ts">
import { getText } from "@/helpers";
import { useLocale } from "@/composables/useLocale";

const { locale, locales, setLocale } = useLocale();
</script>

<template>
  <div class="language" role="group" :aria-label="getText({ key: 'footer.language' })">
    <button
      v-for="option in locales"
      :key="option"
      type="button"
      class="language-option"
      :aria-pressed="locale === option"
      :lang="option"
      @click="setLocale(option)"
    >
      {{ option.toUpperCase() }}
    </button>
  </div>
</template>

<style scoped>
/* In the top corner, clear of the floating panels at the bottom. */
.language {
  position: fixed;
  top: 8px;
  right: 8px;
  z-index: 20;
  display: inline-flex;
  gap: 4px;
}
.language-option {
  min-width: 40px;
  min-height: 32px;
  padding: 0 8px;
  color: var(--snake-muted);
  font: inherit;
  background: transparent;
  border: 2px solid var(--snake-line);
  cursor: pointer;
}
.language-option[aria-pressed="true"] {
  color: var(--snake-green);
  border-color: var(--snake-green);
}
.language-option:hover,
.language-option:focus-visible {
  color: var(--snake-green);
}
</style>
