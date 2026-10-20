<script setup lang="ts">
import { computed } from "vue";
import { useRoute } from "vue-router";
import { hasAccountToken } from "@/composables/useAccount";
import { openProfile, profileModal } from "@/composables/useProfileModal";
import { getText } from "@/helpers";

/**
 * A small button at the top of every page once someone is signed in, over the game too, that opens the profile in a modal. The account and
 * profile pages are the profile already, so it stays out of their way.
 */
const route = useRoute();
const onProfilePage = computed(() => route.name === "account" || route.name === "profile" || route.name === "account-oauth");
const visible = computed(() => hasAccountToken.value && !onProfilePage.value && !profileModal.open.value);
</script>

<template>
  <VBtn
    v-if="visible"
    class="profile-button"
    icon="mdi-account-circle"
    size="small"
    color="primary"
    :aria-label="getText({ key: 'profile.open' })"
    :title="getText({ key: 'profile.open' })"
    data-test="profile-button"
    @click="openProfile()"
  />
</template>

<style scoped>
/* Top centre: the corners and the bottom of the game screen are taken by its panels. Above the canvas and the panels. */
.profile-button {
  position: fixed;
  top: calc(8px + env(safe-area-inset-top, 0px));
  left: 50%;
  transform: translateX(-50%);
  z-index: 1100;
}
</style>
