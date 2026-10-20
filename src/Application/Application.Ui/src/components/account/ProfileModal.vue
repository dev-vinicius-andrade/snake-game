<script setup lang="ts">
import { useRouter } from "vue-router";
import AccountSettings from "@/components/account/AccountSettings.vue";
import ProfileStats from "@/components/account/ProfileStats.vue";
import { useAccount } from "@/composables/useAccount";
import { closeProfile, profileModal } from "@/composables/useProfileModal";
import { getText } from "@/helpers";

/**
 * The profile of the signed in player, over whatever page is open, the game canvas included: the stats and achievements, and the account
 * settings. The game does not read the keyboard or the mouse while it is open (see useSnakeSession).
 */
const router = useRouter();
const accounts = useAccount();

/** No session (never signed in, or the token was refused): the account page is where to sign in. */
async function toSignIn() {
  closeProfile();
  await router.push({ name: "account" });
}

async function signOut() {
  await accounts.logout();
  closeProfile();
}
</script>

<template>
  <VDialog
    :model-value="profileModal.open.value"
    max-width="900"
    scrollable
    data-test="profile-modal"
    @update:model-value="(open: boolean) => !open && closeProfile()"
  >
    <VCard v-if="profileModal.open.value" :title="getText({ key: 'profile.title' })">
      <VTabs v-model="profileModal.tab.value" grow>
        <VTab value="stats" data-test="tab-stats">{{ getText({ key: "profile.statsTab" }) }}</VTab>
        <VTab value="account" data-test="tab-account">{{ getText({ key: "profile.accountTab" }) }}</VTab>
      </VTabs>
      <VCardText>
        <ProfileStats v-if="profileModal.tab.value === 'stats'" @no-account="toSignIn" @sign-in="toSignIn" />
        <AccountSettings v-else @sign-in="toSignIn" />
      </VCardText>
      <VCardActions>
        <VBtn data-test="modal-sign-out" @click="signOut">{{ getText({ key: "account.signOut" }) }}</VBtn>
        <VSpacer />
        <VBtn color="primary" data-test="modal-close" @click="closeProfile">{{ getText({ key: "profile.close" }) }}</VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
</template>
