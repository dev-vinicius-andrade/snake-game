<script setup lang="ts">
import { computed, onMounted } from "vue";
import { useRouter } from "vue-router";
import LanguageSwitcher from "@/components/brand/LanguageSwitcher.vue";
import SiteFooter from "@/components/brand/SiteFooter.vue";
import SnakeLogo from "@/components/brand/SnakeLogo.vue";
import RoomActions from "@/components/rooms/RoomActions.vue";
import { tokenRejected, useAccount } from "@/composables/useAccount";
import { isGuestNicknameAllowed } from "@/game/accountName";
import { getText } from "@/helpers";
import { usePlayerStore } from "@/store/usePlayerStore";

const router = useRouter();
const playerStore = usePlayerStore();

const accounts = useAccount();
/** Signed in: the account name replaces the nickname, and the manager sets it on join. */
const signedIn = computed(() => accounts.status.value === "signedIn" && accounts.account.value !== null);
onMounted(() => void accounts.loadMe());

const nickname = computed({
  get: () => playerStore.data?.nickname ?? "",
  set: (value) => playerStore.setNickname(value),
});
const nicknameAllowed = computed(() => isGuestNicknameAllowed(nickname.value));
const nicknameError = computed(() => (nicknameAllowed.value ? "" : getText({ key: "account.guestNickname" })));
const isPlayerNameValid = computed(() => signedIn.value || (nickname.value.trim().length > 0 && nicknameAllowed.value));

function playNow() {
  if (!isPlayerNameValid.value) return;
  return router.push({ name: "/game/snake/2d" });
}
</script>

<template>
  <main class="home">
    <SnakeLogo />
    <p class="tagline">{{ getText({ key: "home.tagline" }) }}</p>

    <section class="card pixel-card" aria-label="Play">
      <div v-if="signedIn" class="signed-in" data-test="signed-in">
        <div class="text-caption">{{ getText({ key: "profile.playingAs" }) }}</div>
        <div class="text-h6" data-test="account-display">{{ accounts.account.value?.displayName }}</div>
        <div class="signed-in-actions">
          <RouterLink :to="{ name: 'profile' }" data-test="profile-link">{{ getText({ key: "profile.link" }) }}</RouterLink>
          <button type="button" class="link-button" data-test="sign-out" @click="accounts.logout()">
            {{ getText({ key: "account.signOut" }) }}
          </button>
        </div>
      </div>
      <template v-else>
        <VAlert v-if="tokenRejected" type="warning" variant="tonal" role="alert" data-test="session-ended">
          {{ getText({ key: "profile.sessionEnded" }) }}
        </VAlert>
        <VTextField
          v-model="nickname"
          :label="getText({ key: 'variations.nickname.Text' })"
          :error-messages="nicknameError"
          maxlength="20"
          autocomplete="nickname"
          clearable
          autofocus
          @keyup.enter="playNow"
        />
      </template>
      <VBtn block size="large" :disabled="!isPlayerNameValid" @click="playNow">
        {{ getText({ key: "buttons.joinRandomRoom" }) }}
      </VBtn>
      <RoomActions :disabled="!isPlayerNameValid" />
    </section>

    <RouterLink v-if="!signedIn" class="account-link" :to="{ name: 'account' }">{{ getText({ key: "account.link" }) }}</RouterLink>

    <LanguageSwitcher />
    <EyeSetup />
    <SiteFooter />
  </main>
</template>

<style scoped>
.home {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 20px;
  min-height: 100vh;
  min-height: 100dvh;
  padding: max(24px, env(safe-area-inset-top)) 16px max(24px, env(safe-area-inset-bottom));
  box-sizing: border-box;
}
.account-link {
  color: var(--snake-muted);
  font-size: 0.95rem;
}
.signed-in {
  display: flex;
  flex-direction: column;
  gap: 4px;
}
.signed-in-actions {
  display: flex;
  gap: 16px;
  font-size: 0.95rem;
}
.link-button {
  padding: 0;
  color: var(--snake-muted);
  text-decoration: underline;
  background: none;
  border: 0;
  cursor: pointer;
}
.tagline {
  margin: -4px 0 0;
  color: var(--snake-muted);
  font-size: 0.95rem;
  text-align: center;
}
.card {
  display: flex;
  flex-direction: column;
  gap: 16px;
  width: 100%;
  max-width: 440px;
  padding: 20px;
  box-sizing: border-box;
}
</style>
