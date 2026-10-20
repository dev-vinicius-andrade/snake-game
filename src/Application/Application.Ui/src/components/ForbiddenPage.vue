<script setup lang="ts">
import { ref } from "vue";
import { useRouter } from "vue-router";
import { getText } from "@/helpers";

/**
 * "You may not be here": for a page that is only for some players (the admin page). It says why, and where to go. A way in that is not the
 * normal one (an admin key) can be put in the `key` slot: it stays out of sight until asked for.
 */
const props = defineProps<{ signedIn: boolean }>();

const router = useRouter();
const keyShown = ref(false);

const toSignIn = () => router.push({ name: "account" });
const toGame = () => router.push({ name: "home" });
</script>

<template>
  <div class="forbidden text-center py-6" data-test="forbidden">
    <VIcon size="48" aria-hidden="true">mdi-lock-outline</VIcon>
    <h2 class="text-h5 mt-2" data-test="forbidden-title">{{ getText({ key: "forbidden.title" }) }}</h2>
    <p class="mt-2 mb-4" data-test="forbidden-text">{{ getText({ key: props.signedIn ? "forbidden.noRole" : "forbidden.signIn" }) }}</p>
    <div class="actions">
      <VBtn v-if="!props.signedIn" color="primary" data-test="forbidden-sign-in" @click="toSignIn">{{ getText({ key: "account.title" }) }}</VBtn>
      <VBtn data-test="forbidden-back" @click="toGame">{{ getText({ key: "account.back" }) }}</VBtn>
    </div>
    <template v-if="$slots.key">
      <button v-if="!keyShown" type="button" class="link-button mt-6" data-test="forbidden-use-key" @click="keyShown = true">
        {{ getText({ key: "forbidden.useKey" }) }}
      </button>
      <div v-else class="key mt-4" data-test="forbidden-key"><slot name="key" /></div>
    </template>
  </div>
</template>

<style scoped>
.actions {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  justify-content: center;
}
.key {
  max-width: 360px;
  margin-inline: auto;
  text-align: left;
}
.link-button {
  background: none;
  border: 0;
  padding: 0;
  color: inherit;
  font: inherit;
  font-size: 0.85rem;
  text-decoration: underline;
  cursor: pointer;
  opacity: 0.8;
}
</style>
