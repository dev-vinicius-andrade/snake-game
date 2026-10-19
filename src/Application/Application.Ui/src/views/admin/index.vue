<script setup lang="ts">
import { useIntervalFn } from "@vueuse/core";
import { computed, onMounted, ref } from "vue";
import GameplayEditor from "@/components/admin/GameplayEditor.vue";
import RoomGameplayDialog from "@/components/admin/RoomGameplayDialog.vue";
import RoomPerksDialog from "@/components/admin/RoomPerksDialog.vue";
import { useAdminGameplay } from "@/composables/useAdminGameplay";
import { useAdminRooms, type AdminRoom } from "@/composables/useAdminRooms";
import { getText } from "@/helpers";

const REFRESH_MS = 5000;
const admin = useAdminRooms();
/** One gameplay editor for the Gameplay tab and for the perks of a room, so what is edited in one shows in the other. */
const gameplay = useAdminGameplay();
const perksOf = ref<AdminRoom | null>(null);
const gameplayOf = ref<AdminRoom | null>(null);
const keyInput = ref("");
const tab = ref<"rooms" | "gameplay">("rooms");
const confirming = ref<AdminRoom | null>(null);

/** A refresh (status "loading") must not put the sign-in form back: the page would lose what is being edited. */
const signedIn = computed(() => admin.status.value === "ready" || admin.status.value === "loading" || (admin.status.value === "error" && admin.rooms.value.length > 0));
const errorText = computed(() => (admin.error.value ? getText({ key: `admin.${admin.error.value}` }) : ""));

function ageOf(entry: AdminRoom) {
  const minutes = Math.max(0, Math.round((Date.now() - new Date(entry.createdAt).getTime()) / 60000));
  return getText({ key: "admin.minutes", params: { count: minutes } });
}

function playersOf(entry: AdminRoom) {
  return entry.maxPlayers > 0 ? `${entry.players} / ${entry.maxPlayers}` : String(entry.players);
}

async function signIn() {
  await admin.signIn(keyInput.value);
  keyInput.value = "";
}

async function openPerks(entry: AdminRoom) {
  if (!gameplay.draft.value) await gameplay.load();
  perksOf.value = entry;
}

async function confirmDelete() {
  const target = confirming.value;
  confirming.value = null;
  if (target) await admin.remove(target.id);
}

// The room list refreshes on its own; the gameplay editor must be left alone while it is being edited.
useIntervalFn(() => {
  if (tab.value === "rooms") void admin.refresh();
}, REFRESH_MS);
onMounted(() => void admin.refresh());
</script>

<template>
  <VContainer>
    <VRow justify="center">
      <VCol cols="12" md="10">
        <VCard :title="getText({ key: 'admin.title' })">
          <VCardText v-if="!signedIn">
            <VTextField
              v-model="keyInput"
              type="password"
              autocomplete="off"
              :label="getText({ key: 'admin.adminKey' })"
              variant="outlined"
              @keyup.enter="signIn"
            />
            <div v-if="errorText" class="text-error" role="alert">{{ errorText }}</div>
            <VBtn color="primary" :disabled="!keyInput.trim()" @click="signIn">{{ getText({ key: "admin.signIn" }) }}</VBtn>
          </VCardText>

          <VTabs v-if="signedIn" v-model="tab">
            <VTab value="rooms">{{ getText({ key: "admin.tabRooms" }) }}</VTab>
            <VTab value="gameplay">{{ getText({ key: "admin.tabGameplay" }) }}</VTab>
          </VTabs>

          <VCardText v-if="signedIn && tab === 'gameplay'">
            <GameplayEditor :gameplay="gameplay" />
          </VCardText>

          <VCardText v-else-if="signedIn">
            <div v-if="errorText" class="text-error mb-2" role="alert">{{ errorText }}</div>
            <div v-if="admin.rooms.value.length === 0">{{ getText({ key: "admin.empty" }) }}</div>
            <VTable v-else density="compact">
              <thead>
                <tr>
                  <th>{{ getText({ key: "admin.colName" }) }}</th>
                  <th>{{ getText({ key: "admin.colCode" }) }}</th>
                  <th>{{ getText({ key: "admin.colPlayers" }) }}</th>
                  <th>{{ getText({ key: "admin.colAge" }) }}</th>
                  <th>{{ getText({ key: "admin.colServer" }) }}</th>
                  <th />
                  <th />
                  <th />
                </tr>
              </thead>
              <tbody>
                <tr v-for="entry in admin.rooms.value" :key="entry.id">
                  <td>{{ entry.name || getText({ key: "admin.public" }) }}</td>
                  <td>{{ entry.code ?? "-" }}</td>
                  <td>{{ playersOf(entry) }}</td>
                  <td>{{ ageOf(entry) }}</td>
                  <td>{{ entry.server }}</td>
                  <td><VBtn size="small" @click="openPerks(entry)">{{ getText({ key: "admin.roomPerks" }) }}</VBtn></td>
                  <td><VBtn size="small" data-room-gameplay @click="gameplayOf = entry">{{ getText({ key: "gameplayPicker.button" }) }}</VBtn></td>
                  <td><VBtn size="small" color="error" @click="confirming = entry">{{ getText({ key: "admin.delete" }) }}</VBtn></td>
                </tr>
              </tbody>
            </VTable>
          </VCardText>

          <VCardActions v-if="signedIn">
            <VBtn @click="admin.refresh">{{ getText({ key: "admin.refresh" }) }}</VBtn>
            <VSpacer />
            <VBtn @click="admin.signOut">{{ getText({ key: "admin.signOut" }) }}</VBtn>
          </VCardActions>
        </VCard>
      </VCol>
    </VRow>

    <RoomPerksDialog :target="perksOf" :gameplay="gameplay" @close="perksOf = null" />
    <RoomGameplayDialog :target="gameplayOf" :admin="admin" @close="gameplayOf = null" />

    <VDialog :model-value="confirming !== null" max-width="420" @update:model-value="confirming = null">
      <VCard :title="getText({ key: 'admin.delete' })">
        <VCardText>{{ getText({ key: "admin.confirmDelete" }) }}</VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn @click="confirming = null">{{ getText({ key: "buttons.cancel" }) }}</VBtn>
          <VBtn color="error" @click="confirmDelete">{{ getText({ key: "admin.delete" }) }}</VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </VContainer>
</template>
