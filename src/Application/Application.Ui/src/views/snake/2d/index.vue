<template>
  <VLayout>
    <GameRoom>
      <VContainer>
        <VRow>
          <VCol cols="12">
            <GameCanvas />
          </VCol>
        </VRow>
      </VContainer>
    </GameRoom>
  </VLayout>
</template>
<script setup lang="ts">
export interface ISnake2DProperties {}
export interface ISnake2DComponentData {}
const signalR = useSignalR();

// const componentProperties = withDefaults(defineProps<ISnake2DProperties>(), {});
// const componentData = reactive<ISnake2DComponentData>({});
</script>
