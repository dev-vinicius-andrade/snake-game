<template>
  <div :style="playerInfoStyle"></div>
  <div :style="rankingStyle"></div>
  <VContainer fluid class="ma-0 pa-0">
    <VRow style="position-fixed">
      <VCol>{{ roomStore.data?.id }} </VCol>
      <VCol>{{ playerStore.data?.nickname }} </VCol>
    </VRow>
    <slot />
  </VContainer>
</template>
<script setup lang="ts">
import type { CSSProperties } from "vue";
const roomStore = useRoomStore();
const playerStore = usePlayerStore();
const rankingStyle = ref<CSSProperties>({
  position: "fixed",
  top: "5px",
  right: "5px",
  width: "15%",
  height: "20%",
  "background-color": "rgba(0, 0, 0, 0.1)",
  "z-index": 1000,
});
const playerInfoStyle = ref<CSSProperties>({
  position: "fixed",
  top: "5px",
  left: "5px",
  width: "15%",
  height: "10%",
  "background-color": "rgba(0, 0, 0, 0.1)",
  "z-index": 1000,
});

// export interface IGameRoomProperties {
//   roomId?: string;
//   nickname?: string;
// }
// export interface IGameRoomComponentData {
//   roomId?: string;
//   nickname?: string;
// }
// const componentProperties = withDefaults(
//   defineProps<IGameRoomProperties>(),
//   {}
// );
// const componentData = reactive<IGameRoomComponentData>({
//   roomId: componentProperties?.roomId,
//   nickname: componentProperties?.nickname,
// });
</script>
