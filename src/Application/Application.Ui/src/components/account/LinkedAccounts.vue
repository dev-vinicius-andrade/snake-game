<script setup lang="ts">
import { computed } from "vue";
import { getText } from "@/helpers";
import type { SocialProvider } from "@/composables/useAccount";
import ProviderLogo from "./ProviderLogo.vue";
import SocialLoginButton from "./SocialLoginButton.vue";

/** The social accounts of a signed in player: which are linked, and a button for each one that can still be. Presses are the page's to act on. */
const props = defineProps<{ providers: SocialProvider[]; linked: string[]; busy?: boolean }>();
const emit = defineEmits<{ (event: "link", provider: string): void }>();

const rows = computed(() => props.providers.map((provider) => ({ ...provider, isLinked: props.linked.includes(provider.id) })));
</script>

<template>
  <section v-if="rows.length > 0" class="linked-accounts" data-test="linked-accounts">
    <div class="text-subtitle-2 mb-1">{{ getText({ key: "account.linkedTitle" }) }}</div>
    <div class="text-caption mb-3">{{ getText({ key: "account.linkedHint" }) }}</div>
    <div class="list">
      <template v-for="row in rows" :key="row.id">
        <div v-if="row.isLinked" class="done" :data-test="`linked-${row.id}`">
          <ProviderLogo :id="row.id" />
          <span>{{ getText({ key: "account.linkedWith", params: { provider: row.title } }) }}</span>
        </div>
        <SocialLoginButton
          v-else
          :id="row.id"
          :title="row.title"
          :label="getText({ key: 'account.linkWith', params: { provider: row.title } })"
          :disabled="busy"
          @press="emit('link', row.id)"
        />
      </template>
    </div>
  </section>
</template>

<style scoped>
.list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.done {
  display: flex;
  align-items: center;
  gap: 12px;
  min-height: 44px;
  padding: 8px 16px;
  border: 1px solid currentColor;
  border-radius: 4px;
  font-size: 13px;
  opacity: 0.85;
}
</style>
