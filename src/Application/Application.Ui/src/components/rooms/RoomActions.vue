<script setup lang="ts">
import { computed, ref } from "vue";
import { useRouter } from "vue-router";
import RoomGameplayPicker from "@/components/rooms/RoomGameplayPicker.vue";
import { useRoomCreation } from "@/composables/useRoomCreation";
import { choicesBody, defaultChoices } from "@/game/roomGameplay";
import { getText } from "@/helpers";
import { isValidInviteCode, normalizeInviteCode } from "@/game/invite";

/** Under the nickname on the start page: make a private room to play with friends, or join one with an invite code. */
const props = defineProps<{ disabled: boolean }>();

const router = useRouter();
const creation = useRoomCreation();
const dialog = ref(false);
const roomName = ref("");
const maxPlayers = ref(0);
const gameplay = ref(defaultChoices());
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
    const created = await creation.create({ name: roomName.value, maxPlayers: maxPlayers.value, gameplay: choicesBody(gameplay.value) });
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
  <div class="actions">
    <VBtn block color="secondary" :disabled="disabled" @click="dialog = true">
      {{ getText({ key: "rooms.create" }) }}
    </VBtn>

    <div class="divider" role="separator">
      <span>{{ getText({ key: "rooms.orCode" }) }}</span>
    </div>

    <div class="code-row">
      <VTextField
        v-model="codeInput"
        :label="getText({ key: 'rooms.code' })"
        :error-messages="codeError"
        maxlength="40"
        autocomplete="off"
        autocapitalize="characters"
        clearable
        @keyup.enter="canJoinWithCode && joinWithCode()"
      />
      <VBtn :disabled="!canJoinWithCode" @click="joinWithCode">{{ getText({ key: "buttons.join" }) }}</VBtn>
    </div>
  </div>

  <VDialog v-model="dialog" max-width="560" width="92vw" persistent>
    <VCard :title="getText({ key: 'rooms.createTitle' })">
      <VCardText class="dialog-body">
        <VTextField v-model="roomName" :label="getText({ key: 'rooms.name' })" maxlength="32" />
        <VSelect v-model="maxPlayers" :items="playerChoices" :label="getText({ key: 'rooms.maxPlayers' })" />
        <VExpansionPanels variant="accordion">
          <VExpansionPanel>
            <VExpansionPanelTitle>{{ getText({ key: "gameplayPicker.title" }) }}</VExpansionPanelTitle>
            <VExpansionPanelText>
              <RoomGameplayPicker v-model="gameplay" />
            </VExpansionPanelText>
          </VExpansionPanel>
        </VExpansionPanels>
        <div v-if="creating" class="note" role="status">{{ getText({ key: "rooms.creating" }) }}</div>
        <div v-else-if="creation.error.value" class="note error" role="alert">{{ creation.error.value }}</div>
      </VCardText>
      <VCardActions class="dialog-actions">
        <VBtn variant="text" color="default" @click="close">{{ getText({ key: "buttons.cancel" }) }}</VBtn>
        <VSpacer />
        <VBtn :disabled="creating" @click="createRoom">{{ getText({ key: "rooms.createAction" }) }}</VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
</template>

<style scoped>
.actions {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.divider {
  display: flex;
  align-items: center;
  gap: 12px;
  color: var(--snake-muted);
  font-size: 0.8rem;
  text-transform: uppercase;
  letter-spacing: 0.08em;
}
.divider::before,
.divider::after {
  content: "";
  flex: 1;
  height: 2px;
  background: var(--snake-line);
}
.code-row {
  display: flex;
  align-items: flex-start;
  gap: 12px;
}
.code-row .v-text-field {
  flex: 1;
  min-width: 0;
}
.code-row .v-btn {
  margin-top: 1px;
}
.dialog-body {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.note {
  font-size: 0.9rem;
  color: var(--snake-muted);
}
.note.error {
  color: var(--snake-red);
}
.dialog-actions {
  gap: 8px;
}
</style>
