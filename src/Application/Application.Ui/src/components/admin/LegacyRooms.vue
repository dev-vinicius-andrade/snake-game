<script setup lang="ts">
import { computed } from "vue";
import type { HasRoomLists } from "@/composables/useAdminGameplay";
import { hasRoomLists } from "@/composables/useAdminGameplay";
import { getText } from "@/helpers";

/**
 * A perk, prize or event written before rooms had a configuration of their own may still carry lists of rooms. They are obeyed for a
 * room that made no choice, so nothing changes under anyone's feet, but they are not edited any more: this says so and lets them go.
 */
const props = defineProps<{ item: HasRoomLists }>();
const emit = defineEmits<{ (event: "clear"): void }>();

const present = computed(() => hasRoomLists(props.item));
const summary = computed(() =>
  [
    props.item.rooms.length > 0 ? getText({ key: "admin.gameplay.legacyOnly", params: { rooms: props.item.rooms.join(", ") } }) : "",
    props.item.excludedRooms.length > 0 ? getText({ key: "admin.gameplay.legacyNot", params: { rooms: props.item.excludedRooms.join(", ") } }) : "",
  ]
    .filter(Boolean)
    .join(" ")
);
</script>

<template>
  <VAlert v-if="present" type="info" variant="tonal" density="compact" class="mt-2" data-legacy-rooms>
    <div>{{ getText({ key: "admin.gameplay.legacyTitle" }) }} {{ summary }}</div>
    <VBtn size="x-small" variant="text" class="mt-1" @click="emit('clear')">{{ getText({ key: "admin.gameplay.legacyClear" }) }}</VBtn>
  </VAlert>
</template>
