<script setup lang="ts">
import { RouterLink } from "vue-router";
import ProfileButton from "@/components/account/ProfileButton.vue";
import { REPOSITORY_URL } from "@/config/links";
import { hasAccountToken } from "@/composables/useAccount";
import { getText } from "@/helpers";
</script>

<template>
  <footer class="footer">
    <template v-if="hasAccountToken">
      <ProfileButton variant="link" />
      <span aria-hidden="true">·</span>
    </template>
    <RouterLink :to="{ name: 'about' }">{{ getText({ key: "footer.about" }) }}</RouterLink>
    <span aria-hidden="true">·</span>
    <RouterLink :to="{ name: 'terms' }" data-test="footer-terms">{{ getText({ key: "footer.terms" }) }}</RouterLink>
    <span aria-hidden="true">·</span>
    <RouterLink :to="{ name: 'privacy' }" data-test="footer-privacy">{{ getText({ key: "footer.privacy" }) }}</RouterLink>
    <span aria-hidden="true">·</span>
    <a :href="REPOSITORY_URL" target="_blank" rel="noopener noreferrer">
      <VIcon size="small" class="mr-1">mdi-github</VIcon>{{ getText({ key: "footer.source" }) }}
    </a>
    <span aria-hidden="true">·</span>
    <RouterLink :to="{ name: 'credits' }" data-test="footer-credits">{{ getText({ key: "footer.credits" }) }}</RouterLink>
  </footer>
</template>

<style scoped>
.footer {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: center;
  gap: 10px;
  margin-top: auto;
  padding: 12px 0;
  color: var(--snake-muted);
  font-size: 0.85rem;
}
.footer a {
  display: inline-flex;
  align-items: center;
  min-height: 32px;
  color: var(--snake-muted);
  text-decoration: none;
  border-bottom: 2px solid var(--snake-line);
}
.footer a:hover,
.footer a:focus-visible {
  color: var(--snake-green);
  border-color: var(--snake-green);
}
</style>
