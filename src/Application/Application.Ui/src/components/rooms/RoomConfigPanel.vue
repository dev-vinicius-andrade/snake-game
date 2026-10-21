<script setup lang="ts">
import { computed } from "vue";
import { INTENSITY_PRESETS, toggleOf, withToggle, type Preset, type RoomGameplayChoices, type ToggleKind, type ToggleState } from "@/game/roomGameplay";
import { EVENT_TOGGLES, type RoomToggleCatalog } from "@/game/roomToggles";
import { getText } from "@/helpers";
import ToggleRow from "./ToggleRow.vue";

/**
 * What one room has of the things the administrator made: which perks, prizes, rewards and world events are on there, and how strong the
 * storms and the boss are. Nothing is created or edited here, a room only switches what is in the catalog. A thing left on "default"
 * follows what the administrator set for every room.
 */
const props = defineProps<{ modelValue: RoomGameplayChoices; catalog: RoomToggleCatalog }>();
const emit = defineEmits<{ (event: "update:modelValue", value: RoomGameplayChoices): void }>();

const masterItems = ["inherit", "off", "normal"].map((value) => ({ value, title: getText({ key: `gameplayPicker.presets.${value === "normal" ? "on" : value}` }) }));
const intensityItems = INTENSITY_PRESETS.map((value) => ({ value, title: getText({ key: `gameplayPicker.presets.${value}` }) }));

function toggle(kind: ToggleKind, id: string, state: ToggleState) {
  emit("update:modelValue", withToggle(props.modelValue, kind, id, state));
}

function preset(feature: string, value: Preset | null) {
  emit("update:modelValue", { ...props.modelValue, [feature]: value ?? "inherit" });
}

const intensityOf = (feature: string): Preset => {
  const value = props.modelValue[feature];
  return INTENSITY_PRESETS.includes(value) ? value : "inherit";
};

const events = computed(() =>
  props.catalog.events.map((event) => {
    const known = EVENT_TOGGLES.find((candidate) => candidate.id === event.id);
    return { ...event, label: getText({ key: `gameplayPicker.features.${known?.trigger ?? event.id}` }) };
  })
);

const sections = computed(() => [
  { kind: "perk" as const, title: "perks", items: props.catalog.perks, master: "perks", blocked: !props.catalog.perksEnabled },
  { kind: "prize" as const, title: "prizes", items: props.catalog.prizes, master: "prizes", blocked: !props.catalog.prizesEnabled },
  { kind: "reward" as const, title: "rewards", items: props.catalog.rewards, master: null, blocked: false },
]);
</script>

<template>
  <div class="config" data-room-config>
    <p class="config-hint">{{ getText({ key: "gameplayPicker.hint" }) }}</p>

    <section v-for="section in sections" :key="section.kind" class="config-section" :data-section="section.kind">
      <div class="config-head">
        <h3>{{ getText({ key: `gameplayPicker.sections.${section.title}` }) }}</h3>
        <VSelect
          v-if="section.master"
          :model-value="modelValue[section.master] ?? 'inherit'"
          :items="masterItems"
          item-title="title"
          item-value="value"
          density="compact"
          hide-details
          :label="getText({ key: 'gameplayPicker.allOfThem' })"
          :data-feature="section.master"
          class="config-master"
          @update:model-value="(value: Preset | null) => preset(section.master!, value)"
        />
      </div>
      <p v-if="section.blocked" class="config-blocked" role="note">{{ getText({ key: "gameplayPicker.switchedOffEverywhere" }) }}</p>
      <p v-else-if="section.items.length === 0" class="config-empty">{{ getText({ key: "gameplayPicker.nothingMade" }) }}</p>
      <ToggleRow
        v-for="item in section.items"
        :key="item.id"
        :label="item.name"
        :state="toggleOf(modelValue, section.kind, item.id)"
        :on-by-default="item.onByDefault"
        :legacy="item.legacy"
        :blocked="section.blocked"
        :data-item="`${section.kind}:${item.id}`"
        @update:state="(state: ToggleState) => toggle(section.kind, item.id, state)"
      />
    </section>

    <section class="config-section" data-section="event">
      <div class="config-head">
        <h3>{{ getText({ key: "gameplayPicker.sections.events" }) }}</h3>
      </div>
      <p v-if="!catalog.worldEventsEnabled" class="config-blocked" role="note">{{ getText({ key: "gameplayPicker.switchedOffEverywhere" }) }}</p>
      <ToggleRow
        v-for="event in events"
        :key="event.id"
        :label="event.label"
        :state="toggleOf(modelValue, 'event', event.id)"
        :on-by-default="event.onByDefault"
        :legacy="event.legacy"
        :blocked="!catalog.worldEventsEnabled"
        :data-item="`event:${event.id}`"
        @update:state="(state: ToggleState) => toggle('event', event.id, state)"
      >
        <VSelect
          v-if="event.intensity"
          :model-value="intensityOf(event.id)"
          :items="intensityItems"
          item-title="title"
          item-value="value"
          density="compact"
          hide-details
          :label="getText({ key: 'gameplayPicker.intensity' })"
          :data-feature="event.id"
          class="config-intensity"
          @update:model-value="(value: Preset | null) => preset(event.id, value)"
        />
      </ToggleRow>
    </section>
  </div>
</template>

<style scoped>
.config-hint {
  margin: 0 0 10px;
  font-size: 0.85rem;
  color: var(--snake-muted);
}
.config-section {
  margin-top: 14px;
}
.config-head {
  display: flex;
  flex-wrap: wrap;
  gap: 8px 16px;
  align-items: center;
  justify-content: space-between;
}
.config-head h3 {
  margin: 0;
  font-size: 1rem;
}
.config-master {
  max-width: 220px;
}
.config-intensity {
  flex: 0 0 150px;
  margin-left: 8px;
}
.config-empty,
.config-blocked {
  margin: 6px 0;
  font-size: 0.85rem;
  opacity: 0.7;
}
</style>
