<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { cleanUsername, formatDisplay, parseTag } from "@/game/accountName";
import { getText } from "@/helpers";

/**
 * The name and tag the owner signs in with (Name#09291). Changing them is allowed when the new handle is free; the recovery phrase stays the
 * same. Saving is the page's to do: this only checks what was typed and says `save`.
 */
const props = defineProps<{ username: string; tag: number; busy?: boolean; problem?: string; saved?: boolean }>();
const emit = defineEmits<{ (event: "save", username: string, tag: number | undefined): void }>();

const name = ref(props.username);
const tag = ref(String(props.tag).padStart(5, "0"));

watch(
  () => [props.username, props.tag] as const,
  ([username, current]) => {
    name.value = username;
    tag.value = String(current).padStart(5, "0");
  }
);

const tagNumber = computed(() => parseTag(tag.value));
const tagInvalid = computed(() => tag.value.trim() !== "" && tagNumber.value === null);
const cleaned = computed(() => cleanUsername(name.value));
const unchanged = computed(() => cleaned.value === props.username && (tagNumber.value ?? props.tag) === props.tag);
const canSave = computed(() => !props.busy && cleaned.value.length > 0 && !tagInvalid.value && !unchanged.value);
const preview = computed(() => (cleaned.value && tagNumber.value !== null ? formatDisplay(cleaned.value, tagNumber.value) : ""));

function save() {
  if (canSave.value) emit("save", cleaned.value, tagNumber.value ?? undefined);
}
</script>

<template>
  <VForm class="change-handle" data-test="change-handle" @submit.prevent="save">
    <div class="text-subtitle-2 mb-1">{{ getText({ key: "account.handleTitle" }) }}</div>
    <div class="text-caption mb-2">{{ getText({ key: "account.handleHint" }) }}</div>
    <VTextField v-model="name" :label="getText({ key: 'account.username' })" maxlength="20" autocomplete="off" class="mb-1" data-test="handle-name" />
    <VTextField
      v-model="tag"
      :label="getText({ key: 'account.tag' })"
      :error="tagInvalid"
      maxlength="6"
      inputmode="numeric"
      autocomplete="off"
      :hint="getText({ key: 'account.handleTagHint' })"
      data-test="handle-tag"
    />
    <div v-if="preview && !unchanged" class="text-caption mt-1" data-test="handle-preview">{{ getText({ key: "account.handlePreview", params: { handle: preview } }) }}</div>
    <div v-if="problem" class="text-error text-caption mt-1" role="alert" data-test="handle-problem">{{ problem }}</div>
    <div v-if="saved" class="text-success text-caption mt-1" role="status" data-test="handle-saved">{{ getText({ key: "account.handleSaved" }) }}</div>
    <VBtn type="submit" size="small" variant="tonal" class="mt-2" :disabled="!canSave" :loading="busy" data-test="save-handle">
      {{ getText({ key: "account.handleSave" }) }}
    </VBtn>
  </VForm>
</template>
