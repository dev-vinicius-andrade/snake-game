<template>
  <VApp>
    <VMain class="pa-0">
      <RouterView />
    </VMain>
    <ProfileModal />
  </VApp>
</template>

<script setup lang="ts">
import ProfileModal from "@/components/account/ProfileModal.vue";
</script>
