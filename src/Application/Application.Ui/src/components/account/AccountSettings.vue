<script setup lang="ts">
import { onMounted, ref, watch } from "vue";
import { AccountError, useAccount } from "@/composables/useAccount";
import ChangeHandleForm from "@/components/account/ChangeHandleForm.vue";
import LinkedAccounts from "@/components/account/LinkedAccounts.vue";
import { getText } from "@/helpers";

/**
 * What a signed in player can change about their account: the name other players see, the name and tag they sign in with, and the social
 * accounts linked to it. Used on the account page and in the profile modal, so it keeps its own account state and loads it when it shows.
 */
const emit = defineEmits<{ (event: "sign-in"): void }>();

const accounts = useAccount();
const { status, account } = accounts;

/** A short message that says a change went through; it was easy to leave wondering. */
const toast = ref({ open: false, text: "" });
const notify = (text: string) => (toast.value = { open: true, text });

const newDisplayName = ref("");
const linking = ref(false);
const linkProblem = ref("");

/** The manager makes the address of the provider's page (and keeps the state of the link); the browser then goes there. */
async function link(provider: string) {
  linking.value = true;
  linkProblem.value = "";
  try {
    window.location.assign(await accounts.startLink(provider));
  } catch (failure) {
    if (!(failure instanceof AccountError)) throw failure;
    linkProblem.value = getText({ key: `account.errors.${failure.code}` });
    linking.value = false;
  }
}

const handleSaved = ref(false);
const handleProblem = ref("");
const handleBusy = ref(false);

async function saveHandle(username: string, tag: number | undefined) {
  handleSaved.value = false;
  handleProblem.value = "";
  handleBusy.value = true;
  try {
    await accounts.changeHandle(username, tag);
    handleSaved.value = true;
    notify(getText({ key: "account.handleSaved" }));
  } catch (failure) {
    if (!(failure instanceof AccountError)) throw failure;
    handleProblem.value = getText({ key: failure.code === "throttled" ? "account.errors.throttledNow" : `account.errors.${failure.code}` });
  } finally {
    handleBusy.value = false;
  }
}

const displayNameSaved = ref(false);
const displayNameProblem = ref("");

async function saveDisplayName() {
  displayNameSaved.value = false;
  displayNameProblem.value = "";
  try {
    await accounts.changeDisplayName(newDisplayName.value);
    displayNameSaved.value = true;
    notify(getText({ key: "account.displayNameSaved" }));
  } catch (failure) {
    if (!(failure instanceof AccountError)) throw failure;
    displayNameProblem.value = getText({ key: `account.errors.${failure.code}` });
  }
}

watch(account, (current) => (newDisplayName.value = current?.displayName ?? ""), { immediate: true });

onMounted(() => {
  void accounts.loadMe();
  void accounts.loadProviders();
});
</script>

<template>
  <div data-test="account-settings">
    <div v-if="status === 'loading' && !account" role="status">{{ getText({ key: "account.loading" }) }}</div>
    <template v-else-if="status === 'signedIn' && account">
        <div>{{ getText({ key: "account.signedInAs" }) }}</div>
        <div class="text-h5 my-2" data-test="signed-in-display">{{ account.display }}</div>
        <div class="text-caption mb-4" data-test="handle-private">{{ getText({ key: "account.handlePrivate" }) }}</div>
        <VForm class="mb-4" @submit.prevent="saveDisplayName">
          <VTextField
            v-model="newDisplayName"
            :label="getText({ key: 'account.displayName' })"
            :hint="getText({ key: 'account.displayNameHint' })"
            :error-messages="displayNameProblem"
            persistent-hint
            maxlength="20"
            autocomplete="nickname"
            data-test="display-name"
          />
          <div v-if="displayNameSaved" class="text-success text-caption mt-1" role="status">{{ getText({ key: "account.displayNameSaved" }) }}</div>
          <VBtn type="submit" size="small" variant="tonal" class="mt-2" data-test="save-display-name">{{ getText({ key: "account.displayNameSave" }) }}</VBtn>
        </VForm>
        <ChangeHandleForm
          class="mb-4"
          :username="account.username"
          :tag="account.tag"
          :busy="handleBusy"
          :problem="handleProblem"
          :saved="handleSaved"
          @save="saveHandle"
        />
        <LinkedAccounts class="mb-4" :providers="accounts.providers.value" :linked="account.linked" :busy="linking" @link="link" />
        <div v-if="linkProblem" class="text-error text-caption mb-4" role="alert" data-test="link-error">{{ linkProblem }}</div>
    </template>
    <template v-else>
      <VAlert type="warning" variant="tonal" role="alert" data-test="account-signed-out">{{ getText({ key: "profile.sessionEnded" }) }}</VAlert>
      <VBtn class="mt-4" color="primary" @click="emit('sign-in')">{{ getText({ key: "account.title" }) }}</VBtn>
    </template>
    <VSnackbar v-model="toast.open" color="success" :timeout="4000" location="bottom" data-test="toast">{{ toast.text }}</VSnackbar>
  </div>
</template>
