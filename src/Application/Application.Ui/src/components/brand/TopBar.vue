<script setup lang="ts">
import { computed } from "vue";
import ProfileButton from "@/components/account/ProfileButton.vue";
import LanguageSwitcher from "@/components/brand/LanguageSwitcher.vue";
import { hasAccountToken } from "@/composables/useAccount";
import { getText } from "@/helpers";

/**
 * The top right corner of a page: the language, and, for someone who is signed in, their profile and a way to sign out. One place for them, so a
 * page that is not the home page does not have to find room for each in its own content.
 */
const props = defineProps<{ signOut?: boolean }>();
const emit = defineEmits<{ (event: "sign-out"): void }>();

/** The sign out is for a signed in player, or whatever else the page can sign out of (an admin key). */
const showSignOut = computed(() => props.signOut === true || hasAccountToken.value);
</script>

<template>
  <div class="top-bar" data-test="top-bar">
    <ProfileButton variant="link" />
    <button v-if="showSignOut" type="button" class="bar-link" data-test="top-bar-sign-out" @click="emit('sign-out')">
      {{ getText({ key: "account.signOut" }) }}
    </button>
    <LanguageSwitcher inline />
  </div>
</template>

<style scoped>
.top-bar {
  position: fixed;
  top: 8px;
  right: 8px;
  z-index: 20;
  display: flex;
  align-items: center;
  gap: 12px;
}
.bar-link {
  min-height: 32px;
  padding: 0;
  color: var(--snake-muted);
  font: inherit;
  font-size: 0.85rem;
  background: none;
  border: 0;
  border-bottom: 2px solid var(--snake-line);
  cursor: pointer;
}
.bar-link:hover,
.bar-link:focus-visible {
  color: var(--snake-green);
  border-color: var(--snake-green);
}
</style>
