<template>
  <VApp>
    <VMain class="pa-0">
      <RouterView />
    </VMain>
    <ProfileButton />
    <ProfileModal />
  </VApp>
</template>

<script setup lang="ts">
import ProfileButton from "@/components/account/ProfileButton.vue";
import ProfileModal from "@/components/account/ProfileModal.vue";
</script>
