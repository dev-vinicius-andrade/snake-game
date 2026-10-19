<script setup lang="ts">
import { computed } from "vue";
import type { AdminRoom } from "@/composables/useAdminRooms";
import type { useAdminGameplay } from "@/composables/useAdminGameplay";
import { perkIcon } from "@/game/perkInfo";
import { perkWorksInRoom, setPerkInRoom } from "@/game/perkRooms";
import { getText } from "@/helpers";

/** The perks of one room: tick the ones that work there. What is ticked is saved into the perks themselves (their room lists). */
const props = defineProps<{ target: AdminRoom | null; gameplay: ReturnType<typeof useAdminGameplay> }>();
const emit = defineEmits<{ (event: "close"): void }>();

const perks = computed(() => props.gameplay.draft.value?.perks.perks ?? []);
const roomName = computed(() => props.target?.name || props.target?.code || props.target?.id.slice(0, 8) || "");

const KNOWN_ERRORS = ["unavailable", "invalidKey"];
const errorText = computed(() => {
  const error = props.gameplay.error.value;
  if (!error) return "";
  return KNOWN_ERRORS.includes(error) ? getText({ key: `admin.${error}` }) : error;
});

function works(index: number) {
  return !!props.target && perkWorksInRoom(perks.value[index], props.target);
}

function toggle(index: number, enabled: boolean | null) {
  const draft = props.gameplay.draft.value;
  if (!props.target || !draft) return;
  draft.perks.perks[index] = setPerkInRoom(draft.perks.perks[index], props.target, enabled === true);
}

async function save() {
  await props.gameplay.save();
  if (!props.gameplay.error.value) emit("close");
}

function cancel() {
  props.gameplay.revert();
  emit("close");
}
</script>

<template>
  <VDialog :model-value="target !== null" max-width="560" @update:model-value="cancel">
    <VCard v-if="target" :title="getText({ key: 'admin.roomPerksTitle', params: { name: roomName } })">
      <VCardText>
        <p class="mb-3">{{ getText({ key: "admin.roomPerksHint" }) }}</p>
        <div v-for="(perk, index) in perks" :key="perk.id" class="room-perk">
          <VCheckbox
            density="compact"
            hide-details
            :model-value="works(index)"
            :label="perk.name || perk.id"
            @update:model-value="(value: boolean | null) => toggle(index, value)"
          />
          <span class="room-perk-icons" aria-hidden="true">
            <VIcon v-for="(entry, position) in perk.effects" :key="position" size="x-small">{{ perkIcon(entry.effect) }}</VIcon>
          </span>
          <VChip v-if="perk.script?.trim()" size="x-small" label>{{ getText({ key: "admin.scripted" }) }}</VChip>
          <span class="room-perk-foods">{{ perk.foodsRequired }}</span>
        </div>
        <div v-if="errorText" class="text-error mt-2" role="alert">{{ errorText }}</div>
      </VCardText>
      <VCardActions>
        <VSpacer />
        <VBtn @click="cancel">{{ getText({ key: "buttons.cancel" }) }}</VBtn>
        <VBtn color="primary" :disabled="!gameplay.dirty.value" @click="save">{{ getText({ key: "admin.gameplay.save" }) }}</VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
</template>

<style scoped>
.room-perk {
  display: flex;
  align-items: center;
  gap: 8px;
}
.room-perk > :first-child {
  flex: 1 1 auto;
}
.room-perk-icons {
  display: inline-flex;
  gap: 2px;
  opacity: 0.8;
}
.room-perk-foods {
  min-width: 24px;
  text-align: right;
  opacity: 0.6;
}
</style>
