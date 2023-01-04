<template>
  <VRow no-gutters justify="center" align="center">
    <VContainer fluid>
      <VRow no-gutters justify="center" align="center">
        <VIcon v-color:[logoColors.vue]>mdi-vuejs</VIcon>
        <VIcon v-color:[logoColors.js]>mdi-language-javascript</VIcon>
        <VIcon v-color:[logoColors.csharp]>mdi-language-csharp</VIcon>
      </VRow>
      <VRow no-gutters justify="center" align="center">
        <span class="text-h2">
          <span v-color:[logoColors.vue]>Vue</span>.<span
            v-color:[logoColors.js]
            >JS</span
          ><span v-color:[logoColors.csharp]>nake</span>
        </span>
      </VRow>
      <VRow no-gutters justify="center" align="center">
        <VCol cols="3">
          <VTextField
            v-model="reactivePlayerNickname"
            :label="getText({ key: 'variations.nickname.Text' })"
            variant="solo"
            clearable
          ></VTextField>
        </VCol>
      </VRow>
      <VRow no-gutters justify="center" align="center">
        <VCol cols="auto" align-self="center">
          <VBtn
            @click="joinSnake2dRandomRoom()"
            :disabled="!isPlayerNameValid"
            >{{ getText({ key: "buttons.joinRandomRoom" }) }}</VBtn
          >
        </VCol>
      </VRow>
    </VContainer>
  </VRow>
</template>
<script setup lang="ts">
export interface IHomeComponentData {
  nickname?: string;
}
const router = useRouter();
const playerStore = usePlayerStore();
const logoSize = "56px";
const logoColors = ref({
  vue: "#3fb27f",
  js: "#f7df1e",
  csharp: "#68217a",
});

const componentData = reactive<IHomeComponentData>({});

const reactivePlayerNickname = computed({
  get: () => playerStore.data?.nickname ?? "",
  set: (playerNickname) => {
    playerStore.setNickname(playerNickname);
  },
});
const isPlayerNameValid = computed(() => {
  return reactivePlayerNickname.value?.trim();
});
function joinSnake2dRandomRoom() {
  router.push({
    name: "/game/snake/2d",
  });
}
</script>
<style scoped lang="scss">
// .v-text-field {
//   input {
//     color: #fff;
//   }
// }
//
</style>
