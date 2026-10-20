<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { AccountError, useAccount } from "@/composables/useAccount";
import { readOAuthOutcome, type OAuthError } from "@/game/socialLogin";
import { getText } from "@/helpers";

/** Where the browser lands when it comes back from a social sign in: the manager sends a ticket, or the reason it failed. */
const route = useRoute();
const router = useRouter();
const accounts = useAccount();

const failure = ref<OAuthError | null>(null);
const working = computed(() => failure.value === null);

/** The ticket is used once and does not stay in the address (or the history). */
const forgetAddress = () => router.replace({ name: "account-oauth", query: {} });

onMounted(async () => {
  const outcome = readOAuthOutcome(route.query);
  await forgetAddress();
  if (outcome.kind !== "ticket") {
    failure.value = outcome.kind === "error" ? outcome.error : "providerFailed";
    return;
  }
  try {
    await accounts.exchangeTicket(outcome.ticket);
    await router.replace({ name: "home" });
  } catch (error) {
    if (!(error instanceof AccountError)) throw error;
    failure.value = error.code === "throttled" ? "throttled" : "invalidTicket";
  }
});
</script>

<template>
  <VRow no-gutters justify="center" align="center" class="pa-4">
    <VCol cols="12" sm="8" md="5" lg="4">
      <VCard :title="getText({ key: 'account.title' })">
        <VCardText>
          <div v-if="working" role="status" data-test="oauth-working">{{ getText({ key: "account.oauthWorking" }) }}</div>
          <VAlert v-else type="error" variant="tonal" role="alert" data-test="oauth-error">{{ getText({ key: `account.oauthErrors.${failure}` }) }}</VAlert>
        </VCardText>
        <VCardActions v-if="!working">
          <VBtn color="primary" :to="{ name: 'account' }">{{ getText({ key: "account.oauthTryAgain" }) }}</VBtn>
        </VCardActions>
      </VCard>
    </VCol>
  </VRow>
</template>
