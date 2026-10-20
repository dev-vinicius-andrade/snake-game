<script setup lang="ts">
import { computed } from "vue";
import { hasAccountToken } from "@/composables/useAccount";
import { openProfile, profileModal } from "@/composables/useProfileModal";
import { getText } from "@/helpers";

/**
 * Opens the profile modal. It is put where the player's identity already is, not floated over a page: the player panel of the game, the
 * footer, the admin page. Nothing is shown to someone who is not signed in.
 */
const props = defineProps<{ variant: "icon" | "link" }>();

const visible = computed(() => hasAccountToken.value && !profileModal.open.value);
</script>

<template>
  <button
    v-if="visible"
    type="button"
    :class="props.variant === 'icon' ? 'profile-icon' : 'profile-link'"
    :aria-label="getText({ key: 'profile.open' })"
    :title="getText({ key: 'profile.open' })"
    data-test="profile-button"
    @click="openProfile()"
  >
    <VIcon v-if="props.variant === 'icon'" size="18" aria-hidden="true">mdi-account-circle</VIcon>
    <template v-else>{{ getText({ key: "profile.link" }) }}</template>
  </button>
</template>

<style scoped>
/* In a panel the game draws over its canvas (which does not take clicks), so this one asks for them. */
.profile-icon {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  color: inherit;
  background: none;
  border: 1px solid var(--snake-line, currentColor);
  border-radius: 4px;
  cursor: pointer;
  pointer-events: auto;
}
.profile-icon:hover,
.profile-icon:focus-visible {
  color: var(--snake-green, currentColor);
  border-color: var(--snake-green, currentColor);
}
.profile-link {
  min-height: 32px;
  padding: 0;
  color: inherit;
  font: inherit;
  background: none;
  border: 0;
  border-bottom: 2px solid var(--snake-line, currentColor);
  cursor: pointer;
}
.profile-link:hover,
.profile-link:focus-visible {
  color: var(--snake-green, currentColor);
  border-color: var(--snake-green, currentColor);
}
</style>
