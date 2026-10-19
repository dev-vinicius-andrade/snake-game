<script setup lang="ts">
import { computed, defineAsyncComponent, onMounted, reactive, ref } from "vue";
import type { ScriptCheck, useAdminGameplay } from "@/composables/useAdminGameplay";
import { getText } from "@/helpers";
import { PERK_EFFECTS, PERK_ICON_CHOICES, perkIconOf, tierLabel } from "@/game/perkInfo";
import { SCRIPT_TEMPLATES } from "@/game/scriptTemplates";
import WorldEventsEditor from "./WorldEventsEditor.vue";

/** The code editor is big, so it is loaded only when a script is opened. */
const ScriptEditor = defineAsyncComponent(() => import("./ScriptEditor.vue"));

const props = defineProps<{ gameplay: ReturnType<typeof useAdminGameplay> }>();
const gameplay = props.gameplay;
const draft = gameplay.draft;

/** The part of the gameplay config on show. Each is its own tab, so nothing needs a long scroll. */
type Section = "perks" | "prizes" | "events" | "rooms";
const section = ref<Section>("perks");

/** Perks start closed and open one at a time as wanted: indexes of the open ones. */
const openPerks = ref<number[]>([]);

/** The name of the tier a row stands for: the first row is the second tier, since the perk itself is the first. */
const tierName = (index: number) => tierLabel(index + 2);

function addPerk() {
  gameplay.addPerk();
  openPerks.value = [...openPerks.value, (draft.value?.perks.perks.length ?? 1) - 1];
}

function removePerk(index: number) {
  gameplay.removePerk(index);
  openPerks.value = openPerks.value.filter((open) => open !== index).map((open) => (open > index ? open - 1 : open));
}

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
    <div class="save-bar">
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

    <VTabs v-model="section" density="compact" class="mb-4" show-arrows>
      <VTab value="perks">{{ getText({ key: "admin.gameplay.perksTitle" }) }}</VTab>
      <VTab value="prizes">{{ getText({ key: "admin.gameplay.prizeTitle" }) }}</VTab>
      <VTab value="events">{{ getText({ key: "admin.gameplay.worldEventsTitle" }) }}</VTab>
      <VTab value="rooms">{{ getText({ key: "admin.gameplay.roomsTitle" }) }}</VTab>
    </VTabs>

    <VCard v-show="section === 'perks'" variant="outlined" class="mb-4">
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
          <VCol cols="6" md="4">
            <VTextField v-model.number="draft.perks.killFoodsCredit" type="number" density="compact" hide-details :label="getText({ key: 'admin.gameplay.killFoodsCredit' })" />
          </VCol>
        </VRow>

        <VExpansionPanels v-model="openPerks" multiple class="perk-panels">
          <VExpansionPanel v-for="(perk, index) in draft.perks.perks" :key="index" :value="index" :class="{ 'is-off': !perk.enabled }">
            <VExpansionPanelTitle>
              <VIcon size="small" class="mr-2">{{ perkIconOf(perk) }}</VIcon>
              <strong>{{ perk.name || perk.id }}</strong>
              <span class="perk-sub">{{ perk.id }} · {{ perk.foodsRequired }}</span>
              <VChip v-if="!perk.enabled" size="x-small" label color="warning" class="ml-2">{{ getText({ key: "admin.perkOff" }) }}</VChip>
              <VChip v-if="perk.script?.trim()" size="x-small" label class="ml-2">{{ getText({ key: "admin.scripted" }) }}</VChip>
            </VExpansionPanelTitle>
            <VExpansionPanelText>
          <div class="perk-row">
            <VSwitch v-model="perk.enabled" color="primary" density="compact" hide-details :label="getText({ key: 'admin.gameplay.perkEnabled' })" class="switch-field" />
            <VTextField v-model="perk.id" density="compact" hide-details :label="getText({ key: 'admin.gameplay.perkId' })" />
            <VTextField v-model="perk.name" density="compact" hide-details :label="getText({ key: 'admin.gameplay.perkName' })" />
            <VTextField v-model.number="perk.foodsRequired" type="number" density="compact" hide-details :label="getText({ key: 'admin.gameplay.perkFoods' })" />
            <VBtn size="small" variant="text" color="error" @click="removePerk(index)">{{ getText({ key: "admin.gameplay.removePerk" }) }}</VBtn>
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

          <div v-for="(tier, tierIndex) in perk.tiers" :key="`tier-${tierIndex}`" class="perk-row perk-effect" data-tier>
            <strong class="perk-tier-name">{{ tierName(tierIndex) }}</strong>
            <VTextField v-model.number="tier.foodsRequired" type="number" density="compact" hide-details :label="getText({ key: 'admin.gameplay.tierFoods' })" />
            <VTextField v-model.number="tier.multiplier" type="number" step="0.1" density="compact" hide-details :label="getText({ key: 'admin.gameplay.tierMultiplier' })" />
            <VBtn size="small" variant="text" @click="gameplay.removeTier(index, tierIndex)">{{ getText({ key: "admin.gameplay.removeTier" }) }}</VBtn>
          </div>
          <VBtn size="x-small" variant="text" class="mt-1" :disabled="(perk.tiers?.length ?? 0) >= 4" @click="gameplay.addTier(index)">{{ getText({ key: "admin.gameplay.addTier" }) }}</VBtn>

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

          <div class="field-grid">
            <VCombobox
              v-model="perk.icon"
              :items="PERK_ICON_CHOICES"
              :prepend-inner-icon="perkIconOf(perk)"
              density="compact"
              hide-details
              clearable
              :label="getText({ key: 'admin.gameplay.perkIcon' })"
            >
              <template #item="{ props: item }">
                <VListItem v-bind="item" :prepend-icon="item.title" />
              </template>
            </VCombobox>
            <VTextField
              v-model="perk.description"
              density="compact"
              hide-details
              maxlength="200"
              :label="getText({ key: 'admin.gameplay.perkDescription' })"
            />
          </div>

          <div class="perk-script">
            <VBtn v-if="!perk.script" size="x-small" variant="text" @click="gameplay.addScript(index)">{{ getText({ key: "admin.gameplay.addScript" }) }}</VBtn>
            <template v-else>
              <div class="perk-script-head">
                <strong>{{ getText({ key: "admin.gameplay.scriptTitle" }) }}</strong>
                <VMenu>
                  <template #activator="{ props: menu }">
                    <VBtn v-bind="menu" size="x-small" variant="text">{{ getText({ key: "admin.gameplay.scriptTemplate" }) }}</VBtn>
                  </template>
                  <VList density="compact">
                    <VListItem
                      v-for="template in SCRIPT_TEMPLATES"
                      :key="template.id"
                      :title="getText({ key: `admin.gameplay.scriptTemplates.${template.id}.name` })"
                      :subtitle="getText({ key: `admin.gameplay.scriptTemplates.${template.id}.hint` })"
                      @click="perk.script = template.code"
                    />
                  </VList>
                </VMenu>
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
            </VExpansionPanelText>
          </VExpansionPanel>
        </VExpansionPanels>

        <VBtn class="mt-2" size="small" @click="addPerk">{{ getText({ key: "admin.gameplay.addPerk" }) }}</VBtn>
      </VCardText>
    </VCard>

    <VCard v-show="section === 'prizes'" variant="outlined" class="mb-4">
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

    <WorldEventsEditor v-show="section === 'events'" :gameplay="gameplay" />

    <VCard v-show="section === 'rooms'" variant="outlined" class="mb-4">
      <VCardTitle>{{ getText({ key: "admin.gameplay.roomsTitle" }) }}</VCardTitle>
      <VCardText>
        <div class="field-grid">
          <VTextField v-model.number="draft.rooms.emptyPrivateRoomMinutes" type="number" density="compact" hide-details :label="getText({ key: 'admin.gameplay.emptyRoomMinutes' })" />
          <VTextField v-model.number="draft.rooms.maxPrivateRoomMinutes" type="number" density="compact" hide-details :label="getText({ key: 'admin.gameplay.maxRoomMinutes' })" />
        </div>
      </VCardText>
    </VCard>
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
  flex-wrap: wrap;
  align-items: center;
  gap: 8px;
  margin-bottom: 4px;
}
.perk-script-warning {
  margin-bottom: 6px;
  font-size: 12px;
  color: var(--snake-amber);
}
.save-bar {
  position: sticky;
  top: 0;
  z-index: 5;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 8px 16px;
  padding: 8px 0;
  margin-bottom: 8px;
  background: var(--snake-surface);
  border-bottom: 1px solid var(--snake-line);
}
.perk-panels :deep(.v-expansion-panel) {
  background: transparent;
  border: 1px solid var(--snake-line);
  margin-bottom: 8px;
}
.perk-panels :deep(.v-expansion-panel.is-off) {
  opacity: 0.6;
}
.perk-sub {
  margin-left: 12px;
  opacity: 0.7;
  font-size: 12px;
}
.perk-card.is-off {
  opacity: 0.55;
}
/* Fields side by side as far as the screen allows, then onto more lines: nothing is squeezed. */
.field-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 8px;
  align-items: center;
  margin-top: 8px;
}
.switch-field {
  flex: 0 0 auto !important;
}
</style>
