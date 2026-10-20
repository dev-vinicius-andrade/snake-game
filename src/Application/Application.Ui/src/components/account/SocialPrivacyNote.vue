<script setup lang="ts">
import { getText } from "@/helpers";
</script>

<template>
  <VAlert type="info" variant="tonal" density="compact" icon="mdi-shield-lock-outline" class="privacy-note" data-test="social-privacy">
    {{ getText({ key: "account.socialPrivacy" }) }}
    <RouterLink :to="{ name: 'privacy' }" data-test="privacy-link">{{ getText({ key: "legal.privacy" }) }}</RouterLink>
  </VAlert>
</template>

<style scoped>
.privacy-note {
  font-size: 12px;
  line-height: 1.4;
}
</style>
