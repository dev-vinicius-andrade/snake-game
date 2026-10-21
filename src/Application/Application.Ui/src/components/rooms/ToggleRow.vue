<script setup lang="ts">
import type { ToggleState } from "@/game/roomGameplay";
import { getText } from "@/helpers";

/** One thing a room can switch: its name, whether it is on in a room that changes nothing, and a choice of default, on or off. */
defineProps<{
  label: string;
  state: ToggleState;
  /** Whether it is on in a room that changes nothing. */
  onByDefault: boolean;
  /** It still carries an old room list. */
  legacy?: boolean;
  /** The master switch above it is off, so nothing here can run. */
  blocked?: boolean;
}>();
const emit = defineEmits<{ (event: "update:state", value: ToggleState): void }>();
</script>

<template>
  <div class="toggle-row" :class="{ blocked }" data-toggle-row>
    <div class="toggle-name">
      <span>{{ label }}</span>
      <VChip size="x-small" label :color="onByDefault ? 'success' : undefined" class="ml-2">
        {{ getText({ key: onByDefault ? "gameplayPicker.onByDefault" : "gameplayPicker.offByDefault" }) }}
      </VChip>
      <VChip v-if="legacy" size="x-small" label color="info" class="ml-1">{{ getText({ key: "gameplayPicker.oldList" }) }}</VChip>
      <slot />
    </div>
    <VBtnToggle
      :model-value="state"
      mandatory
      density="compact"
      variant="outlined"
      divided
      :disabled="blocked"
      @update:model-value="(value: ToggleState) => emit('update:state', value)"
    >
      <VBtn value="inherit" size="small" data-state="inherit">{{ getText({ key: "gameplayPicker.presets.inherit" }) }}</VBtn>
      <VBtn value="on" size="small" color="success" data-state="on">{{ getText({ key: "gameplayPicker.presets.on" }) }}</VBtn>
      <VBtn value="off" size="small" color="error" data-state="off">{{ getText({ key: "gameplayPicker.presets.off" }) }}</VBtn>
    </VBtnToggle>
  </div>
</template>

<style scoped>
.toggle-row {
  display: flex;
  flex-wrap: wrap;
  gap: 6px 12px;
  align-items: center;
  justify-content: space-between;
  padding: 4px 0;
  border-bottom: 1px solid var(--snake-line);
}
.toggle-row.blocked {
  opacity: 0.5;
}
.toggle-name {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  align-items: center;
  min-width: 0;
  flex: 1 1 180px;
}
</style>
