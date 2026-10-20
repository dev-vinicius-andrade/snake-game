<script setup lang="ts">
import { computed } from "vue";
import { getText } from "@/helpers";
import { providerLook } from "@/game/socialLogin";
import ProviderLogo from "./ProviderLogo.vue";

/**
 * "Continue with ..." (or the label it is given) in the colours of the provider. With an `href` it is a plain link: the provider's page
 * takes over the whole window. Without one it is a button that says `press`, for what has to be asked of the manager first.
 */
const props = defineProps<{ id: string; title: string; href?: string; label?: string; disabled?: boolean }>();
const emit = defineEmits<{ (event: "press"): void }>();

const look = computed(() => providerLook(props.id));
const style = computed(() => ({
  "--social-bg": look.value.background,
  "--social-text": look.value.text,
  "--social-border": look.value.border,
  "--social-hover": look.value.hover,
}));
</script>

<template>
  <component
    :is="href ? 'a' : 'button'"
    :href="href"
    :type="href ? undefined : 'button'"
    :disabled="href ? undefined : disabled"
    class="social-button"
    :style="style"
    :data-test="`social-${id}`"
    :data-provider="id"
    @click="href ? undefined : emit('press')"
  >
    <ProviderLogo :id="id" />
    <span class="label">{{ label ?? getText({ key: "account.continueWith", params: { provider: title } }) }}</span>
  </component>
</template>

<style scoped>
.social-button {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 12px;
  min-height: 44px;
  padding: 8px 16px;
  color: var(--social-text);
  background: var(--social-bg);
  border: 1px solid var(--social-border);
  border-radius: 4px;
  font-family: inherit;
  font-size: 13px;
  line-height: 1.3;
  text-align: center;
  text-decoration: none;
  cursor: pointer;
  width: 100%;
  transition: background-color 0.15s;
}
.social-button:hover:not(:disabled),
.social-button:focus-visible {
  background: var(--social-hover);
}
.social-button:disabled {
  opacity: 0.6;
  cursor: default;
}
.social-button:focus-visible {
  outline: 2px solid #ffffff;
  outline-offset: 2px;
}
.label {
  min-width: 0;
}
</style>
