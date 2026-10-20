<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { useRouter } from "vue-router";
import { AccountError, useAccount } from "@/composables/useAccount";
import { cleanUsername, formatDisplay, parseTag } from "@/game/accountName";
import { getText } from "@/helpers";

const MIN_OWN_PHRASE = 12;
const AVAILABILITY_DELAY_MS = 400;

const router = useRouter();
const accounts = useAccount();
const { status, account, error } = accounts;

const tab = ref<"register" | "login">("register");
const busy = computed(() => status.value === "loading");

// Register
const regName = ref("");
const regDisplayName = ref("");
const regTag = ref("");
const phraseMode = ref<"give" | "own">("give");
const ownPhrase = ref("");
const availability = ref<{ available: boolean; name: string } | null>(null);
let availabilityTimer: ReturnType<typeof setTimeout> | undefined;

// Sign in
const loginName = ref("");
const loginTag = ref("");
const loginPhrase = ref("");

// Shown once after registering
const created = ref<{ display: string; phrase: string } | null>(null);
const savedConfirmed = ref(false);
const copied = ref<"" | "name" | "phrase">("");

const regTagNumber = computed(() => parseTag(regTag.value));
const regTagInvalid = computed(() => regTag.value.trim() !== "" && regTagNumber.value === null);
const ownPhraseShort = computed(() => phraseMode.value === "own" && ownPhrase.value.length < MIN_OWN_PHRASE);
const canRegister = computed(
  () => !busy.value && cleanUsername(regName.value).length > 0 && !regTagInvalid.value && !ownPhraseShort.value
);
const loginTagNumber = computed(() => parseTag(loginTag.value));
const canLogin = computed(
  () => !busy.value && cleanUsername(loginName.value).length > 0 && loginTagNumber.value !== null && loginPhrase.value.length > 0
);

const errorMessage = computed(() => {
  const failure = error.value;
  if (!failure) return "";
  if (failure.code === "throttled" && failure.retryAfterSeconds !== undefined) {
    return getText({ key: "account.errors.throttled", params: { seconds: failure.retryAfterSeconds } });
  }
  return getText({ key: failure.code === "throttled" ? "account.errors.throttledNow" : `account.errors.${failure.code}` });
});

const availabilityMessage = computed(() => {
  if (!availability.value) return "";
  const key = availability.value.available ? "account.availableYes" : "account.availableNo";
  return getText({ key, params: { name: availability.value.name } });
});

function scheduleAvailabilityCheck() {
  clearTimeout(availabilityTimer);
  availability.value = null;
  const username = cleanUsername(regName.value);
  if (!username || regTagInvalid.value) return;
  availabilityTimer = setTimeout(async () => {
    try {
      const result = await accounts.checkAvailability(username, regTagNumber.value ?? undefined);
      if (username === cleanUsername(regName.value)) availability.value = { available: result.available, name: formatNameAsKept(result.username) };
    } catch {
      availability.value = null;
    }
  }, AVAILABILITY_DELAY_MS);
}

function formatNameAsKept(kept: string): string {
  const tag = regTagNumber.value;
  return tag === null ? kept : formatDisplay(kept, tag);
}

async function register() {
  if (!canRegister.value) return;
  try {
    const phrase = await accounts.register({
      username: cleanUsername(regName.value),
      tag: regTagNumber.value ?? undefined,
      phrase: phraseMode.value === "own" ? ownPhrase.value : undefined,
      displayName: regDisplayName.value,
    });
    created.value = { display: account.value?.display ?? "", phrase };
    savedConfirmed.value = false;
    ownPhrase.value = "";
  } catch (failure) {
    if (!(failure instanceof AccountError)) throw failure;
  }
}

// Signed in: the name other players see
const newDisplayName = ref("");
const displayNameSaved = ref(false);
const displayNameProblem = ref("");

async function saveDisplayName() {
  displayNameSaved.value = false;
  displayNameProblem.value = "";
  try {
    await accounts.changeDisplayName(newDisplayName.value);
    displayNameSaved.value = true;
  } catch (failure) {
    if (!(failure instanceof AccountError)) throw failure;
    displayNameProblem.value = getText({ key: `account.errors.${failure.code}` });
  }
}

watch(account, (current) => (newDisplayName.value = current?.displayName ?? ""), { immediate: true });

async function login() {
  const tag = loginTagNumber.value;
  if (!canLogin.value || tag === null) return;
  try {
    await accounts.login({ username: cleanUsername(loginName.value), tag, phrase: loginPhrase.value });
    loginPhrase.value = "";
  } catch (failure) {
    if (!(failure instanceof AccountError)) throw failure;
  }
}

async function copy(which: "name" | "phrase") {
  const text = which === "name" ? created.value?.display : created.value?.phrase;
  if (!text) return;
  try {
    await navigator.clipboard.writeText(text);
    copied.value = which;
  } catch {
    copied.value = "";
  }
}

function finish() {
  created.value = null;
  return router.replace({ name: "home" });
}

function back() {
  return router.push({ name: "home" });
}

watch([regName, regTag], scheduleAvailabilityCheck);
onMounted(() => void accounts.loadMe());
onBeforeUnmount(() => clearTimeout(availabilityTimer));
</script>

<template>
  <VRow no-gutters justify="center" align="center" class="pa-4">
    <VCol cols="12" sm="8" md="5" lg="4">
      <VCard :title="getText({ key: 'account.title' })">
        <VCardText>
          <!-- Shown once, right after registering -->
          <template v-if="created">
            <h2 class="text-h6 mb-2">{{ getText({ key: "account.savedTitle" }) }}</h2>
            <VAlert type="warning" variant="tonal" class="mb-4">{{ getText({ key: "account.savedWarning" }) }}</VAlert>
            <div class="text-caption">{{ getText({ key: "account.yourName" }) }}</div>
            <div class="secret mb-2">
              <code data-test="created-display">{{ created.display }}</code>
              <VBtn size="small" variant="text" @click="copy('name')">
                {{ getText({ key: copied === "name" ? "account.copied" : "account.copy" }) }}
              </VBtn>
            </div>
            <div class="text-caption">{{ getText({ key: "account.yourPhrase" }) }}</div>
            <div class="secret mb-4">
              <code data-test="created-phrase">{{ created.phrase }}</code>
              <VBtn size="small" variant="text" @click="copy('phrase')">
                {{ getText({ key: copied === "phrase" ? "account.copied" : "account.copy" }) }}
              </VBtn>
            </div>
            <VCheckbox v-model="savedConfirmed" :label="getText({ key: 'account.saved' })" hide-details />
          </template>

          <div v-else-if="status === 'loading' && !account" role="status">{{ getText({ key: "account.loading" }) }}</div>

          <!-- Signed in -->
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
            <VBtn variant="tonal" :to="{ name: 'profile' }" data-test="profile-link">{{ getText({ key: "profile.link" }) }}</VBtn>
          </template>

          <!-- Guest -->
          <template v-else>
            <p class="mb-4">{{ getText({ key: "account.intro" }) }}</p>
            <VTabs v-model="tab" grow class="mb-4">
              <VTab value="register">{{ getText({ key: "account.tabRegister" }) }}</VTab>
              <VTab value="login">{{ getText({ key: "account.tabLogin" }) }}</VTab>
            </VTabs>

            <VForm v-if="tab === 'register'" @submit.prevent="register">
              <VTextField
                v-model="regName"
                :label="getText({ key: 'account.username' })"
                :hint="getText({ key: 'account.usernameHint' })"
                persistent-hint
                maxlength="20"
                autocomplete="username"
                class="mb-2"
              />
              <VTextField
                v-model="regDisplayName"
                :label="getText({ key: 'account.displayName' })"
                :hint="getText({ key: 'account.displayNameHint' })"
                persistent-hint
                maxlength="20"
                autocomplete="nickname"
                class="mb-2"
              />
              <VTextField
                v-model="regTag"
                :label="getText({ key: 'account.tag' })"
                :hint="getText({ key: 'account.tagHint' })"
                :error-messages="regTagInvalid ? getText({ key: 'account.errors.invalidTag' }) : ''"
                persistent-hint
                maxlength="6"
                inputmode="numeric"
                autocomplete="off"
                class="mb-2"
              />
              <div v-if="availabilityMessage" class="text-caption mb-2" role="status" data-test="availability">{{ availabilityMessage }}</div>
              <VRadioGroup v-model="phraseMode" :label="getText({ key: 'account.phraseChoice' })">
                <VRadio value="give" :label="getText({ key: 'account.phraseGive' })" />
                <VRadio value="own" :label="getText({ key: 'account.phraseOwn' })" />
              </VRadioGroup>
              <VTextField
                v-if="phraseMode === 'own'"
                v-model="ownPhrase"
                :label="getText({ key: 'account.phraseOwnField' })"
                :hint="getText({ key: 'account.phraseOwnHint' })"
                persistent-hint
                autocomplete="off"
              />
              <VBtn type="submit" block color="primary" class="mt-4" :disabled="!canRegister" :loading="busy">
                {{ getText({ key: "account.submitRegister" }) }}
              </VBtn>
            </VForm>

            <VForm v-else @submit.prevent="login">
              <VTextField v-model="loginName" :label="getText({ key: 'account.username' })" maxlength="20" autocomplete="username" class="mb-2" />
              <VTextField
                v-model="loginTag"
                :label="getText({ key: 'account.tagLogin' })"
                maxlength="6"
                inputmode="numeric"
                autocomplete="off"
                class="mb-2"
              />
              <VTextField v-model="loginPhrase" :label="getText({ key: 'account.phrase' })" type="password" autocomplete="off" />
              <VBtn type="submit" block color="primary" class="mt-4" :disabled="!canLogin" :loading="busy">
                {{ getText({ key: "account.submitLogin" }) }}
              </VBtn>
            </VForm>
          </template>

          <VAlert v-if="errorMessage && !created" type="error" variant="tonal" class="mt-4" role="alert" data-test="account-error">
            {{ errorMessage }}
          </VAlert>
        </VCardText>

        <VCardActions>
          <VBtn @click="back">{{ getText({ key: "account.back" }) }}</VBtn>
          <VSpacer />
          <VBtn v-if="created" color="primary" :disabled="!savedConfirmed" @click="finish">{{ getText({ key: "account.done" }) }}</VBtn>
          <VBtn v-else-if="status === 'signedIn'" @click="accounts.logout()">{{ getText({ key: "account.signOut" }) }}</VBtn>
        </VCardActions>
      </VCard>
    </VCol>
  </VRow>
</template>

<style scoped>
.secret {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
  padding: 8px 12px;
  border: 1px solid currentColor;
  border-radius: 4px;
  overflow-wrap: anywhere;
}
</style>
