<template>
  <VContainer>
    <VRow>
      <VCol cols="12">
        <canvas ref="gameCanvas" :style="componentData.style" />
      </VCol>
    </VRow>
  </VContainer>
</template>
<script lang="ts">
const defaultGameCanvasStyle: CSSProperties = {
  border: "1px solid #000000",
  width: "100%",
};
</script>
<script setup lang="ts">
import type { CSSProperties } from "vue";
export interface IGameCanvasComponentProperties {
  style?: CSSProperties;
}
export interface IGameCanvasComponentData {
  style?: CSSProperties;
}
const roomStore = useRoomStore();
const playerStore = usePlayerStore();

const componentProperties = withDefaults(
  defineProps<IGameCanvasComponentProperties>(),
  {
    style: () => defaultGameCanvasStyle,
  }
);

const componentData = reactive<IGameCanvasComponentData>({
  style: {
    ...defaultGameCanvasStyle,
    ...componentProperties?.style,
  },
});
</script>
