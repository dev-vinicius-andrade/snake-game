<script setup lang="ts">
import { computed } from "vue";
import { useRouter } from "vue-router";
import LanguageSwitcher from "@/components/brand/LanguageSwitcher.vue";
import SiteFooter from "@/components/brand/SiteFooter.vue";
import SnakeLogo from "@/components/brand/SnakeLogo.vue";
import RoomActions from "@/components/rooms/RoomActions.vue";
import { getText } from "@/helpers";
import { usePlayerStore } from "@/store/usePlayerStore";

const router = useRouter();
const playerStore = usePlayerStore();

const nickname = computed({
  get: () => playerStore.data?.nickname ?? "",
  set: (value) => playerStore.setNickname(value),
});
const isPlayerNameValid = computed(() => nickname.value.trim().length > 0);

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
      <VTextField
        v-model="nickname"
        :label="getText({ key: 'variations.nickname.Text' })"
        maxlength="20"
        autocomplete="nickname"
        clearable
        autofocus
        @keyup.enter="playNow"
      />
      <VBtn block size="large" :disabled="!isPlayerNameValid" @click="playNow">
        {{ getText({ key: "buttons.joinRandomRoom" }) }}
      </VBtn>
      <RoomActions :disabled="!isPlayerNameValid" />
    </section>

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
