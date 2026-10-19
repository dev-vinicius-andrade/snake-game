<script setup lang="ts">
import { computed, defineAsyncComponent, onMounted, reactive } from "vue";
import type { ScriptCheck, useAdminGameplay } from "@/composables/useAdminGameplay";
import { getText } from "@/helpers";
import { PERK_EFFECTS } from "@/game/perkInfo";

/** The code editor is big, so it is loaded only when a script is opened. */
const ScriptEditor = defineAsyncComponent(() => import("./ScriptEditor.vue"));

const props = defineProps<{ gameplay: ReturnType<typeof useAdminGameplay> }>();
const gameplay = props.gameplay;
const draft = gameplay.draft;

/** What the last check of each perk's script said, by perk id. */
const checks = reactive<Record<string, ScriptCheck>>({});

async function check(perkId: string, script: string) {
  checks[perkId] = await gameplay.checkScript(script);
}

function checkText(result: ScriptCheck) {
  if (result.ok) return getText({ key: "admin.gameplay.scriptOk" });
  return result.error === "empty" || result.error === "unavailable" ? getText({ key: result.error === "empty" ? "admin.gameplay.scriptEmpty" : "admin.unavailable" }) : (result.error ?? "");
}

/** The server lists the effects it knows; one this build has no text for is still offered, by its name. */
const effectItems = computed(() =>
  (gameplay.effects.value.length > 0 ? gameplay.effects.value : [...PERK_EFFECTS]).map((effect) => ({
    value: effect,
    title: effectName(effect),
  }))
);

const KNOWN_ERRORS = ["unavailable", "invalidKey"];
const errorText = computed(() => {
  const error = gameplay.error.value;
  if (!error) return "";
  return KNOWN_ERRORS.includes(error) ? getText({ key: `admin.${error}` }) : error;
});

function effectName(effect: string) {
  return PERK_EFFECTS.some((known) => known === effect) ? getText({ key: `perkEffects.${effect}.name` }) : effect;
}

function effectHelp(effect: string) {
  return PERK_EFFECTS.some((known) => known === effect) ? getText({ key: `perkEffects.${effect}.help` }) : "";
}

onMounted(() => {
  if (!gameplay.draft.value) void gameplay.load();
});
</script>

<template>
  <div v-if="draft" class="gameplay">
    <VCard variant="outlined" class="mb-4">
      <VCardTitle>{{ getText({ key: "admin.gameplay.perksTitle" }) }}</VCardTitle>
      <VCardText>
        <VRow dense>
          <VCol cols="12" md="4">
            <VSwitch v-model="draft.perks.enabled" color="primary" hide-details :label="getText({ key: 'admin.gameplay.perksOn' })" />
          </VCol>
          <VCol cols="6" md="4">
            <VTextField v-model.number="draft.perks.shotSpreadDegrees" type="number" density="compact" hide-details :label="getText({ key: 'admin.gameplay.spread' })" />
          </VCol>
          <VCol cols="6" md="4">
            <VTextField v-model.number="draft.perks.burstIntervalMilliseconds" type="number" density="compact" hide-details :label="getText({ key: 'admin.gameplay.burstInterval' })" />
          </VCol>
        </VRow>

        <div v-for="(perk, index) in draft.perks.perks" :key="index" class="perk-card" :class="{ 'is-off': !perk.enabled }">
          <div class="perk-row">
            <VSwitch v-model="perk.enabled" color="primary" density="compact" hide-details :label="getText({ key: 'admin.gameplay.perkEnabled' })" class="switch-field" />
            <VTextField v-model="perk.id" density="compact" hide-details :label="getText({ key: 'admin.gameplay.perkId' })" />
            <VTextField v-model="perk.name" density="compact" hide-details :label="getText({ key: 'admin.gameplay.perkName' })" />
            <VTextField v-model.number="perk.foodsRequired" type="number" density="compact" hide-details :label="getText({ key: 'admin.gameplay.perkFoods' })" />
            <VBtn size="small" variant="text" color="error" @click="gameplay.removePerk(index)">{{ getText({ key: "admin.gameplay.removePerk" }) }}</VBtn>
          </div>

          <template v-for="(entry, effectIndex) in perk.effects" :key="effectIndex">
            <div class="perk-row perk-effect">
              <VSelect v-model="entry.effect" :items="effectItems" item-title="title" item-value="value" density="compact" hide-details :label="getText({ key: 'admin.gameplay.perkEffect' })" />
              <VTextField v-model.number="entry.amount" type="number" step="any" density="compact" hide-details :label="getText({ key: 'admin.gameplay.perkAmount' })" />
              <VBtn size="small" variant="text" :disabled="perk.effects.length <= 1 && !perk.script?.trim()" @click="gameplay.removeEffect(index, effectIndex)">
                {{ getText({ key: "admin.gameplay.removeEffect" }) }}
              </VBtn>
              <div class="perk-help">{{ effectHelp(entry.effect) }}</div>
            </div>
          </template>
          <VBtn size="x-small" variant="text" class="mt-1" @click="gameplay.addEffect(index)">{{ getText({ key: "admin.gameplay.addEffect" }) }}</VBtn>

          <VCombobox
            v-model="perk.rooms"
            multiple
            chips
            closable-chips
            density="compact"
            class="mt-2"
            :label="getText({ key: 'admin.gameplay.rooms' })"
            :hint="getText({ key: 'admin.gameplay.roomsHint' })"
            persistent-hint
          />
          <VCombobox
            v-model="perk.excludedRooms"
            multiple
            chips
            closable-chips
            density="compact"
            class="mt-2"
            :label="getText({ key: 'admin.gameplay.excludedRooms' })"
            hide-details
          />

          <div class="perk-script">
            <VBtn v-if="!perk.script" size="x-small" variant="text" @click="gameplay.addScript(index)">{{ getText({ key: "admin.gameplay.addScript" }) }}</VBtn>
            <template v-else>
              <div class="perk-script-head">
                <strong>{{ getText({ key: "admin.gameplay.scriptTitle" }) }}</strong>
                <VBtn size="x-small" variant="text" @click="check(perk.id, perk.script ?? '')">{{ getText({ key: "admin.gameplay.checkScript" }) }}</VBtn>
                <VBtn size="x-small" variant="text" color="error" :disabled="perk.effects.length === 0" @click="gameplay.removeScript(index)">
                  {{ getText({ key: "admin.gameplay.removeScript" }) }}
                </VBtn>
              </div>
              <div v-if="perk.rooms.length === 0" class="perk-script-warning" role="note">{{ getText({ key: "admin.gameplay.scriptOff" }) }}</div>
              <ScriptEditor :model-value="perk.script ?? ''" :label="getText({ key: 'admin.gameplay.scriptTitle' })" @update:model-value="perk.script = $event" />
              <div
                v-if="checks[perk.id]"
                class="mt-1"
                :class="checks[perk.id].ok ? 'text-success' : 'text-error'"
                role="status"
              >
                {{ checkText(checks[perk.id]) }}
              </div>
            </template>
          </div>
        </div>

        <VBtn class="mt-2" size="small" @click="gameplay.addPerk">{{ getText({ key: "admin.gameplay.addPerk" }) }}</VBtn>
      </VCardText>
    </VCard>

    <VCard variant="outlined" class="mb-4">
      <VCardTitle>{{ getText({ key: "admin.gameplay.prizeTitle" }) }}</VCardTitle>
      <VCardText>
        <VSwitch v-model="draft.prize.enabled" color="primary" hide-details :label="getText({ key: 'admin.gameplay.prizeOn' })" />

        <div v-for="(kind, index) in draft.prize.prizes" :key="index" class="perk-card" :class="{ 'is-off': !kind.enabled }">
          <div class="field-grid">
            <VSwitch v-model="kind.enabled" color="primary" density="compact" hide-details :label="getText({ key: 'admin.gameplay.prizeEnabled' })" />
            <VTextField v-model="kind.id" density="compact" hide-details :label="getText({ key: 'admin.gameplay.perkId' })" />
            <VTextField v-model="kind.name" density="compact" hide-details :label="getText({ key: 'admin.gameplay.perkName' })" />
            <VTextField v-model="kind.color" type="color" density="compact" hide-details :label="getText({ key: 'admin.gameplay.prizeColor' })" />
          </div>
          <div class="field-grid">
            <VTextField v-model.number="kind.spawnIntervalSeconds" type="number" density="compact" hide-details :label="getText({ key: 'admin.gameplay.prizeInterval' })" />
            <VTextField v-model.number="kind.lifetimeSeconds" type="number" density="compact" hide-details :label="getText({ key: 'admin.gameplay.prizeLifetime' })" />
            <VTextField v-model.number="kind.value" type="number" density="compact" hide-details :label="getText({ key: 'admin.gameplay.prizeValue' })" />
            <VTextField v-model.number="kind.foodsCredit" type="number" density="compact" hide-details :label="getText({ key: 'admin.gameplay.prizeCredit' })" />
            <VTextField v-model.number="kind.minPlayers" type="number" density="compact" hide-details :label="getText({ key: 'admin.gameplay.prizeMinPlayers' })" />
            <VSelect
              v-model="kind.grantsPerk"
              :items="[{ id: '', name: getText({ key: 'admin.gameplay.prizeGrantsNone' }) }, ...gameplay.perkChoices.value]"
              item-title="name"
              item-value="id"
              density="compact"
              hide-details
              :label="getText({ key: 'admin.gameplay.prizeGrantsPerk' })"
            />
          </div>
          <VCombobox v-model="kind.rooms" multiple chips closable-chips density="compact" class="mt-2" hide-details :label="getText({ key: 'admin.gameplay.prizeRooms' })" />
          <VCombobox v-model="kind.excludedRooms" multiple chips closable-chips density="compact" class="mt-2" hide-details :label="getText({ key: 'admin.gameplay.excludedRooms' })" />
          <VBtn size="small" variant="text" color="error" class="mt-1" :disabled="draft.prize.prizes.length <= 1" @click="gameplay.removePrize(index)">
            {{ getText({ key: "admin.gameplay.prizeRemove" }) }}
          </VBtn>
        </div>
        <VBtn class="mt-3" size="small" @click="gameplay.addPrize">{{ getText({ key: "admin.gameplay.prizeAdd" }) }}</VBtn>
      </VCardText>
    </VCard>

    <VCard variant="outlined" class="mb-4">
      <VCardTitle>{{ getText({ key: "admin.gameplay.roomsTitle" }) }}</VCardTitle>
      <VCardText>
        <div class="field-grid">
          <VTextField v-model.number="draft.rooms.emptyPrivateRoomMinutes" type="number" density="compact" hide-details :label="getText({ key: 'admin.gameplay.emptyRoomMinutes' })" />
          <VTextField v-model.number="draft.rooms.maxPrivateRoomMinutes" type="number" density="compact" hide-details :label="getText({ key: 'admin.gameplay.maxRoomMinutes' })" />
        </div>
      </VCardText>
    </VCard>

    <div v-if="errorText" class="text-error mb-2" role="alert">{{ errorText }}</div>
    <div v-else-if="gameplay.status.value === 'saved' && !gameplay.dirty.value" class="text-success mb-2" role="status">
      {{ getText({ key: "admin.gameplay.saved" }) }}
    </div>
    <div v-else-if="gameplay.dirty.value" class="mb-2" role="status">{{ getText({ key: "admin.gameplay.unsaved" }) }}</div>

    <div class="d-flex flex-wrap ga-2">
      <VBtn color="primary" :disabled="!gameplay.dirty.value" @click="gameplay.save">{{ getText({ key: "admin.gameplay.save" }) }}</VBtn>
      <VBtn :disabled="!gameplay.dirty.value" @click="gameplay.revert">{{ getText({ key: "admin.gameplay.revert" }) }}</VBtn>
      <VBtn @click="gameplay.useDefaults">{{ getText({ key: "admin.gameplay.defaults" }) }}</VBtn>
    </div>
  </div>
  <div v-else-if="errorText" class="text-error" role="alert">{{ errorText }}</div>
</template>

<style scoped>
/* One card per perk. Inside it the fields sit side by side and wrap onto more lines as the screen narrows. */
.perk-card {
  margin-top: 14px;
  padding: 10px 12px 12px;
  border: 1px solid var(--snake-line);
}
.perk-row {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  margin-top: 8px;
}
.perk-row > * {
  flex: 1 1 130px;
  min-width: 0;
}
.perk-row > :last-child:is(button) {
  flex: 0 0 auto;
}
.perk-effect {
  margin-left: 14px;
}
.perk-help {
  flex-basis: 100%;
  margin-top: -2px;
  padding-left: 2px;
  font-size: 12px;
  opacity: 0.65;
}
.perk-script {
  margin-top: 10px;
}
.perk-script-head {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 4px;
}
.perk-script-warning {
  margin-bottom: 6px;
  font-size: 12px;
  color: var(--snake-amber);
}
.perk-card.is-off {
  opacity: 0.55;
}
/* Fields side by side as far as the screen allows, then onto more lines: nothing is squeezed. */
.field-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  gap: 8px;
  align-items: center;
  margin-top: 8px;
}
.switch-field {
  flex: 0 0 auto !important;
}
</style>
