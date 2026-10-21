<script setup lang="ts">
import { computed } from "vue";
import { getText } from "@/helpers";

/** Which rewards of the catalog a prize or an event gives. Every one picked is given; none picked gives no reward. */
const props = defineProps<{
  /** The ids picked. */
  modelValue: string[];
  /** The catalog, by id and name. */
  rewards: Array<{ id: string; name: string }>;
}>();
const emit = defineEmits<{ (event: "update:modelValue", value: string[]): void }>();

/** A pick whose reward is gone from the catalog still shows, so it can be taken away. */
const items = computed(() => [
  ...props.rewards,
  ...props.modelValue.filter((id) => !props.rewards.some((reward) => reward.id === id)).map((id) => ({ id, name: id })),
]);
</script>

<template>
  <VSelect
    :model-value="modelValue"
    :items="items"
    item-title="name"
    item-value="id"
    multiple
    chips
    closable-chips
    density="compact"
    :label="getText({ key: 'admin.gameplay.rewardsPicked' })"
    :hint="rewards.length === 0 ? getText({ key: 'admin.gameplay.rewardsNoneYet' }) : getText({ key: 'admin.gameplay.rewardsPickedHint' })"
    persistent-hint
    data-reward-picker
    @update:model-value="(ids: string[]) => emit('update:modelValue', ids)"
  />
</template>
