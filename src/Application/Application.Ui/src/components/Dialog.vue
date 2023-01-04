<template>
  <slot name="activator" :show="showDialog" :close="closeDialog" />
  <VDialog
    v-model="reactiveShowDialog"
    :class="componentProperties.class"
    :width="width"
  >
    <slot />
  </VDialog>
</template>
<script setup lang="ts">
import { Events } from "@enums";
export interface IDialogComponentProperties {
  showDialog?: boolean;
  class?: string;
}
export interface IDialogComponentData {
  //  showDialog: boolean;
}
export interface IDialogComponentEvents {
  (e: Events.onModelValueUpdate, value?: boolean): void;
}

const componentProperties = withDefaults(
  defineProps<IDialogComponentProperties>(),
  {
    class: "",
    showDialog: false,
  }
);
const componentData = reactive<IDialogComponentData>({});
const emits = defineEmits<IDialogComponentEvents>();
const display = useVuetifyDisplay();
const reactiveShowDialog = computed({
  get: () => componentProperties?.showDialog,
  set: (showDialog?: boolean) => {
    emits(Events.onModelValueUpdate, showDialog);
  },
});
const width = computed(() => (display.smAndUp ? 650 : "auto"));

function showDialog() {
  reactiveShowDialog.value = true;
}
function closeDialog() {
  reactiveShowDialog.value = false;
}
</script>
