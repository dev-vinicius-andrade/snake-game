<script setup lang="ts">
import { computed, onMounted } from "vue";
import { useAdminGameplay } from "@/composables/useAdminGameplay";
import { getText } from "@/helpers";
import { PERK_EFFECTS } from "@/game/perkInfo";

const gameplay = useAdminGameplay();
const draft = gameplay.draft;

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

onMounted(() => void gameplay.load());
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

        <div v-for="(perk, index) in draft.perks.perks" :key="index" class="perk-row">
          <VTextField v-model="perk.id" density="compact" hide-details :label="getText({ key: 'admin.gameplay.perkId' })" />
          <VTextField v-model="perk.name" density="compact" hide-details :label="getText({ key: 'admin.gameplay.perkName' })" />
          <VTextField v-model.number="perk.foodsRequired" type="number" density="compact" hide-details :label="getText({ key: 'admin.gameplay.perkFoods' })" />
          <VSelect v-model="perk.effect" :items="effectItems" item-title="title" item-value="value" density="compact" hide-details :label="getText({ key: 'admin.gameplay.perkEffect' })" />
          <VTextField v-model.number="perk.amount" type="number" step="any" density="compact" hide-details :label="getText({ key: 'admin.gameplay.perkAmount' })" />
          <VBtn size="small" variant="text" color="error" @click="gameplay.removePerk(index)">{{ getText({ key: "admin.gameplay.removePerk" }) }}</VBtn>
          <div class="perk-help">{{ effectHelp(perk.effect) }}</div>
        </div>

        <VBtn class="mt-2" size="small" @click="gameplay.addPerk">{{ getText({ key: "admin.gameplay.addPerk" }) }}</VBtn>
      </VCardText>
    </VCard>

    <VCard variant="outlined" class="mb-4">
      <VCardTitle>{{ getText({ key: "admin.gameplay.prizeTitle" }) }}</VCardTitle>
      <VCardText>
        <VRow dense>
          <VCol cols="12" md="3">
            <VSwitch v-model="draft.prize.enabled" color="primary" hide-details :label="getText({ key: 'admin.gameplay.prizeOn' })" />
          </VCol>
          <VCol cols="6" md="3">
            <VTextField v-model.number="draft.prize.spawnIntervalSeconds" type="number" density="compact" hide-details :label="getText({ key: 'admin.gameplay.prizeInterval' })" />
          </VCol>
          <VCol cols="6" md="3">
            <VTextField v-model.number="draft.prize.lifetimeSeconds" type="number" density="compact" hide-details :label="getText({ key: 'admin.gameplay.prizeLifetime' })" />
          </VCol>
          <VCol cols="4" md="1">
            <VTextField v-model.number="draft.prize.value" type="number" density="compact" hide-details :label="getText({ key: 'admin.gameplay.prizeValue' })" />
          </VCol>
          <VCol cols="4" md="1">
            <VTextField v-model.number="draft.prize.foodsCredit" type="number" density="compact" hide-details :label="getText({ key: 'admin.gameplay.prizeCredit' })" />
          </VCol>
          <VCol cols="4" md="1">
            <VTextField v-model.number="draft.prize.minPlayers" type="number" density="compact" hide-details :label="getText({ key: 'admin.gameplay.prizeMinPlayers' })" />
          </VCol>
        </VRow>
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
/* One row per perk: the fields side by side, wrapping onto more lines as the screen narrows, with what the effect means underneath. */
.perk-row {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  margin-top: 12px;
}
.perk-row > * {
  flex: 1 1 130px;
  min-width: 0;
}
.perk-row > :nth-child(3) {
  flex: 0 1 120px;
}
.perk-row > :nth-child(4) {
  flex: 2 1 190px;
}
.perk-row > :nth-child(6) {
  flex: 0 0 auto;
}
.perk-help {
  flex-basis: 100%;
  margin-top: -2px;
  padding-left: 2px;
  font-size: 12px;
  opacity: 0.65;
}
</style>
