<script setup lang="ts">
import { computed, ref, watch } from "vue";
import RoomConfigPanel from "@/components/rooms/RoomConfigPanel.vue";
import type { useAdminGameplay } from "@/composables/useAdminGameplay";
import { WORLD_EVENT_TRIGGERS, type AdminRoom, type TriggerResult, type useAdminRooms } from "@/composables/useAdminRooms";
import { defaultChoices, parseChoices, type RoomGameplayChoices } from "@/game/roomGameplay";
import { catalogFromConfig, emptyCatalog } from "@/game/roomToggles";
import { getText } from "@/helpers";

/**
 * The configuration of one room: which of the perks, prizes, rewards and world events the administrator made are on there, and how strong
 * the storms and the boss are. It is the one place a room is configured. There is also a way to start a world event in the room right
 * now, to try it or to liven the room up.
 */
const props = defineProps<{ target: AdminRoom | null; admin: ReturnType<typeof useAdminRooms>; gameplay: ReturnType<typeof useAdminGameplay> }>();
const emit = defineEmits<{ (event: "close"): void }>();

const choices = ref<RoomGameplayChoices>(defaultChoices());
const saved = ref<RoomGameplayChoices>(defaultChoices());
const failed = ref(false);
const triggered = ref<{ kind: string; result: TriggerResult } | null>(null);

watch(
  () => props.target,
  (next) => {
    saved.value = parseChoices(next?.gameplay);
    choices.value = { ...saved.value };
    failed.value = false;
    triggered.value = null;
  },
  { immediate: true }
);

const roomName = computed(() => props.target?.name || props.target?.code || props.target?.id.slice(0, 8) || "");
const dirty = computed(() => JSON.stringify(choices.value) !== JSON.stringify(saved.value));
const catalog = computed(() => (props.gameplay.draft.value && props.target ? catalogFromConfig(props.gameplay.draft.value, props.target) : emptyCatalog()));

async function save() {
  if (!props.target) return;
  failed.value = !(await props.admin.setGameplay(props.target.id, choices.value));
  if (!failed.value) emit("close");
}

async function start(kind: string) {
  if (!props.target) return;
  triggered.value = { kind, result: await props.admin.trigger(props.target.id, kind) };
}

const triggerText = computed(() => {
  const result = triggered.value?.result;
  if (!result) return "";
  if (result === "started") return getText({ key: "gameplayPicker.started" });
  if (result === "notNow") return getText({ key: "gameplayPicker.notNow" });
  return getText({ key: `admin.${result}` });
});
</script>

<template>
  <VDialog :model-value="target !== null" max-width="760" scrollable @update:model-value="emit('close')">
    <VCard v-if="target" :title="getText({ key: 'gameplayPicker.roomTitle', params: { name: roomName } })">
      <VCardText>
        <p class="mb-3">{{ getText({ key: "gameplayPicker.roomHint" }) }}</p>
        <RoomConfigPanel v-model="choices" :catalog="catalog" />
        <div v-if="failed" class="text-error mt-2" role="alert">{{ getText({ key: `admin.${admin.error.value ?? "unavailable"}` }) }}</div>

        <p class="mt-5 mb-2">{{ getText({ key: "gameplayPicker.triggerHint" }) }}</p>
        <div class="d-flex flex-wrap ga-2">
          <VBtn v-for="kind in WORLD_EVENT_TRIGGERS" :key="kind" size="small" variant="tonal" :data-trigger="kind" @click="start(kind)">
            {{ getText({ key: "gameplayPicker.trigger" }) }}: {{ getText({ key: `gameplayPicker.features.${kind}` }) }}
          </VBtn>
        </div>
        <div v-if="triggered" class="mt-2" :class="triggered.result === 'started' ? 'text-success' : 'text-warning'" role="status">
          {{ getText({ key: `gameplayPicker.features.${triggered.kind}` }) }}: {{ triggerText }}
        </div>
      </VCardText>
      <VCardActions>
        <VSpacer />
        <VBtn @click="emit('close')">{{ getText({ key: "gameplayPicker.close" }) }}</VBtn>
        <VBtn color="primary" :disabled="!dirty" @click="save">
          {{ getText({ key: "admin.gameplay.save" }) }}
        </VBtn>
      </VCardActions>
    </VCard>
  </VDialog>
</template>
