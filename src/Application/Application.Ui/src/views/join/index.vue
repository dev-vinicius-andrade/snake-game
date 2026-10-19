<script setup lang="ts">
import axios from "axios";
import { computed, onMounted, ref } from "vue";
import { useRouter } from "vue-router";
import { useAccount } from "@/composables/useAccount";
import { resolveManagerUrl } from "@/config/env";
import { getText } from "@/helpers";
import { isGuestNicknameAllowed } from "@/game/accountName";
import { isValidInviteCode, normalizeInviteCode } from "@/game/invite";
import { usePlayerStore } from "@/store/usePlayerStore";

interface RoomPreview {
  name: string | null;
  players: number;
  maxPlayers: number;
}

const props = defineProps<{ code: string }>();
const router = useRouter();
const playerStore = usePlayerStore();
const code = computed(() => normalizeInviteCode(props.code));
const preview = ref<RoomPreview | null>(null);
const state = ref<"loading" | "ready" | "missing">("loading");

const accounts = useAccount();
const signedIn = computed(() => accounts.status.value === "signedIn");

const nickname = computed({
  get: () => playerStore.data?.nickname ?? "",
  set: (value) => playerStore.setNickname(value),
});
const nicknameAllowed = computed(() => isGuestNicknameAllowed(nickname.value));
const canJoin = computed(() => state.value === "ready" && (signedIn.value || (nickname.value.trim().length > 0 && nicknameAllowed.value)));
const players = computed(() => {
  if (!preview.value) return "";
  const key = preview.value.maxPlayers > 0 ? "rooms.players" : "rooms.playersOpen";
  return getText({ key, params: { count: preview.value.players, max: preview.value.maxPlayers } });
});

async function load() {
  if (!isValidInviteCode(code.value)) {
    state.value = "missing";
    return;
  }
  try {
    preview.value = (await axios.get<RoomPreview>(`${resolveManagerUrl()}/rooms/code/${code.value}`)).data;
    state.value = "ready";
  } catch {
    state.value = "missing";
  }
}

function join() {
  return router.replace({ name: "/game/snake/2d", query: { code: code.value } });
}

onMounted(() => {
  void accounts.loadMe();
  return load();
});
</script>

<template>
  <VRow no-gutters justify="center" align="center">
    <VCol cols="12" sm="8" md="4">
      <VCard :title="getText({ key: 'rooms.joinTitle' })">
        <VCardText>
          <div v-if="state === 'loading'" role="status">{{ getText({ key: "rooms.loading" }) }}</div>
          <div v-else-if="state === 'missing'" class="text-error" role="alert">{{ getText({ key: "rooms.notFound" }) }}</div>
          <template v-else>
            <div>{{ getText({ key: "rooms.roomName" }) }}: <strong>{{ preview?.name || code }}</strong></div>
            <div class="mb-4">{{ players }}</div>
            <div v-if="signedIn" data-test="account-display">
              {{ getText({ key: "profile.playingAs" }) }}: <strong>{{ accounts.account.value?.display }}</strong>
            </div>
            <VTextField
              v-else
              v-model="nickname"
              :label="getText({ key: 'variations.nickname.Text' })"
              variant="outlined"
              :error-messages="nicknameAllowed ? '' : getText({ key: 'account.guestNickname' })"
              @keyup.enter="canJoin && join()"
            />
          </template>
        </VCardText>
        <VCardActions>
          <VBtn @click="router.replace({ name: 'home' })">{{ getText({ key: "game.back" }) }}</VBtn>
          <VSpacer />
          <VBtn color="primary" :disabled="!canJoin" @click="join">{{ getText({ key: "buttons.join" }) }}</VBtn>
        </VCardActions>
      </VCard>
    </VCol>
  </VRow>
</template>
