<script setup lang="ts">
import { computed } from "vue";
import { GAMEPLAY_FEATURES, type RoomGameplayChoices } from "@/game/roomGameplay";
import { getText } from "@/helpers";

/** One choice for each feature of the gameplay: the default, off, or (for the world events) calm, normal or wild. */
const props = defineProps<{ modelValue: RoomGameplayChoices }>();
const emit = defineEmits<{ (event: "update:modelValue", value: RoomGameplayChoices): void }>();

const rows = computed(() =>
  GAMEPLAY_FEATURES.map((feature) => ({
    key: feature.key,
    label: getText({ key: `gameplayPicker.features.${feature.key}` }),
    items: feature.presets.map((preset) => ({ value: preset, title: getText({ key: `gameplayPicker.presets.${preset}` }) })),
  }))
);

function choose(feature: string, preset: string) {
  emit("update:modelValue", { ...props.modelValue, [feature]: preset as RoomGameplayChoices[string] });
}
</script>

<template>
  <div class="picker">
    <p class="picker-hint">{{ getText({ key: "gameplayPicker.hint" }) }}</p>
    <div class="picker-grid">
      <VSelect
        v-for="row in rows"
        :key="row.key"
        :model-value="modelValue[row.key] ?? 'inherit'"
        :items="row.items"
        item-title="title"
        item-value="value"
        density="compact"
        hide-details
        :label="row.label"
        :data-feature="row.key"
        @update:model-value="(preset: string) => choose(row.key, preset)"
      />
    </div>
  </div>
</template>

<style scoped>
.picker-hint {
  margin: 0 0 10px;
  font-size: 0.85rem;
  color: var(--snake-muted);
}
.picker-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
  gap: 10px;
}
</style>
