<script setup lang="ts">
import { computed, ref } from "vue";
import { useRouter } from "vue-router";
import { useRoomCreation } from "@/composables/useRoomCreation";
import { getText } from "@/helpers";
import { isValidInviteCode, normalizeInviteCode } from "@/game/invite";

/** Under the nickname on the start page: make a private room to play with friends, or join one with an invite code. */
const props = defineProps<{ disabled: boolean }>();

const router = useRouter();
const creation = useRoomCreation();
const dialog = ref(false);
const roomName = ref("");
const maxPlayers = ref(0);
const codeInput = ref("");

const MAX_PLAYER_CHOICES = [0, 2, 4, 6, 8, 10, 12, 16, 20];
const playerChoices = MAX_PLAYER_CHOICES.map((value) => ({
  value,
  title: value === 0 ? getText({ key: "rooms.anyPlayers" }) : String(value),
}));

const code = computed(() => normalizeInviteCode(codeInput.value));
const codeError = computed(() => (code.value && !isValidInviteCode(code.value) ? getText({ key: "rooms.invalidCode" }) : ""));
const canJoinWithCode = computed(() => !props.disabled && isValidInviteCode(code.value));
const creating = computed(() => creation.status.value === "connecting" || creation.status.value === "waiting");

async function createRoom() {
  try {
    const created = await creation.create({ name: roomName.value, maxPlayers: maxPlayers.value });
    dialog.value = false;
    await router.push({ name: "/game/snake/2d", query: { room: created.roomId, code: created.code } });
  } catch {
    // The reason is shown from creation.error.
  }
}

function joinWithCode() {
  return router.push({ name: "join", params: { code: code.value } });
}

function close() {
  void creation.cancel();
  dialog.value = false;
}
</script>

<template>
  <VRow no-gutters justify="center" align="center" class="mt-4">
    <VCol cols="auto">
      <VBtn :disabled="disabled" @click="dialog = true">{{ getText({ key: "rooms.create" }) }}</VBtn>
    </VCol>
  </VRow>
  <VRow no-gutters justify="center" align="start" class="mt-4">
    <VCol cols="auto" class="code-field">
      <VTextField
        v-model="codeInput"
        :label="getText({ key: 'rooms.haveCode' })"
        :error-messages="codeError"
        variant="solo"
        density="compact"
        clearable
        @keyup.enter="canJoinWithCode && joinWithCode()"
      />
    </VCol>
    <VCol cols="auto" class="ml-2">
      <VBtn :disabled="!canJoinWithCode" @click="joinWithCode">{{ getText({ key: "rooms.joinWithCode" }) }}</VBtn>
    </VCol>
  </VRow>

  <VDialog v-model="dialog" max-width="420" persistent>
    <VCard :title="getText({ key: 'rooms.createTitle' })">
      <VCardText>
        <VTextField v-model="roomName" :label="getText({ key: 'rooms.name' })" maxlength="32" variant="outlined" />
        <VSelect v-model="maxPlayers" :items="playerChoices" :label="getText({ key: 'rooms.maxPlayers' })" variant="outlined" />
        <div v-if="creating" role="status">{{ getText({ key: "rooms.creating" }) }}</div>
        <div v-else-if="creation.error.value" class="text-error" role="alert">{{ creation.error.value }}</div>
      </VCardText>
      <VCardActions>
        <VSpacer />
        <VBtn @click="close">{{ getText({ key: "buttons.cancel" }) }}</VBtn>
        <VBtn :disabled="creating" color="primary" @click="createRoom">{{ getText({ key: "rooms.create" }) }}</VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
</template>

<style scoped>
.code-field {
  width: 260px;
}
</style>
