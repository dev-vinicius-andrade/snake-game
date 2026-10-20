<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import GameIcon from "@/components/game/GameIcon.vue";
import type { AdminAchievement, useAdminAchievements } from "@/composables/useAdminAchievements";
import { useLocale } from "@/composables/useLocale";
import { achievementText, mergeAchievementTexts } from "@/game/achievements";
import { ACHIEVEMENT_TEXTS } from "@/game/achievementTexts";
import { PERK_ICON_CHOICES } from "@/game/perkInfo";
import { getText } from "@/helpers";

/**
 * The achievements of the game: what unlocks each (a stat reaching a threshold), its icon, its texts, and whether it is on. Changes are saved at
 * once. A built-in achievement can be changed too; its words are the site's until one is written here.
 */
const props = defineProps<{ achievements: ReturnType<typeof useAdminAchievements> }>();

const LOCALES = ["en", "pt"] as const;
const { locale } = useLocale();

const editing = ref<AdminAchievement | null>(null);
const isNew = ref(false);
const confirmingDelete = ref<AdminAchievement | null>(null);
const busy = ref(false);

const entries = computed(() => props.achievements.entries.value);
const writable = computed(() => props.achievements.writable.value);
const errorText = computed(() => {
  const code = props.achievements.error.value;
  return code ? getText({ key: `admin.achievements.errors.${code}` }) : "";
});

/** What a player sees for the achievement now: the words written here, else the site's. */
const shownTexts = computed(() => mergeAchievementTexts(ACHIEVEMENT_TEXTS, entries.value));
const nameOf = (id: string) => achievementText(shownTexts.value, locale.value, id).name;

const statLabel = (stat: string) => getText({ key: `profile.stats.${stat}` });

function blank(): AdminAchievement {
  return { id: "", icon: "game-icons:trophy", stat: props.achievements.stats.value[0] ?? "games", threshold: 1, enabled: true, texts: { en: { name: "", description: "" }, pt: { name: "", description: "" } } };
}

/** The texts of the form: what is written here, else the site's own words, so saving a built-in one keeps them. */
function formTexts(entry: AdminAchievement): AdminAchievement["texts"] {
  return Object.fromEntries(
    LOCALES.map((code) => [code, { ...(entry.texts[code] ?? ACHIEVEMENT_TEXTS[code]?.[entry.id] ?? { name: "", description: "" }) }])
  );
}

function add() {
  isNew.value = true;
  editing.value = blank();
}

function edit(entry: AdminAchievement) {
  isNew.value = false;
  editing.value = { ...entry, texts: formTexts(entry) };
}

async function save() {
  if (!editing.value) return;
  busy.value = true;
  const saved = await props.achievements.save({ ...editing.value, id: editing.value.id.trim(), threshold: Number(editing.value.threshold) });
  busy.value = false;
  if (saved) editing.value = null;
}

async function toggle(entry: AdminAchievement) {
  await props.achievements.save({ ...entry, enabled: !entry.enabled, texts: formTexts(entry) });
}

async function confirmDelete() {
  const target = confirmingDelete.value;
  confirmingDelete.value = null;
  if (target) await props.achievements.remove(target.id);
}

onMounted(() => void props.achievements.load());
</script>

<template>
  <div data-test="achievements-editor">
    <VAlert v-if="!writable" type="info" variant="tonal" class="mb-3" data-test="achievements-readonly">{{ getText({ key: "admin.achievements.readOnly" }) }}</VAlert>
    <div v-if="errorText && !editing" class="text-error mb-2" role="alert" data-test="achievements-error">{{ errorText }}</div>

    <div class="d-flex align-center mb-2">
      <div class="text-caption">{{ getText({ key: "admin.achievements.hint" }) }}</div>
      <VSpacer />
      <VBtn size="small" color="primary" :disabled="!writable" data-test="achievement-add" @click="add">{{ getText({ key: "admin.achievements.add" }) }}</VBtn>
    </div>

    <VTable density="compact">
      <thead>
        <tr>
          <th />
          <th>{{ getText({ key: "admin.achievements.colName" }) }}</th>
          <th>{{ getText({ key: "admin.achievements.colRule" }) }}</th>
          <th>{{ getText({ key: "admin.achievements.colOn" }) }}</th>
          <th />
          <th />
        </tr>
      </thead>
      <tbody>
        <tr v-for="entry in entries" :key="entry.id" :class="{ off: !entry.enabled }" :data-test="`achievement-${entry.id}`">
          <td><GameIcon :name="entry.icon" :size="24" /></td>
          <td>
            <div>{{ nameOf(entry.id) }}</div>
            <div class="text-caption id">{{ entry.id }}</div>
          </td>
          <td>{{ statLabel(entry.stat) }} &ge; {{ entry.threshold }}</td>
          <td>
            <VSwitch :model-value="entry.enabled" :disabled="!writable" density="compact" hide-details :aria-label="getText({ key: 'admin.achievements.colOn' })" @update:model-value="toggle(entry)" />
          </td>
          <td><VBtn size="small" :disabled="!writable" :data-test="`achievement-edit-${entry.id}`" @click="edit(entry)">{{ getText({ key: "admin.achievements.edit" }) }}</VBtn></td>
          <td><VBtn size="small" color="error" :disabled="!writable" :data-test="`achievement-delete-${entry.id}`" @click="confirmingDelete = entry">{{ getText({ key: "admin.delete" }) }}</VBtn></td>
        </tr>
      </tbody>
    </VTable>

    <VDialog :model-value="editing !== null" max-width="640" scrollable @update:model-value="editing = null">
      <VCard v-if="editing" :title="getText({ key: isNew ? 'admin.achievements.addTitle' : 'admin.achievements.editTitle' })" data-test="achievement-form">
        <VCardText>
          <VTextField v-model="editing.id" :disabled="!isNew" :label="getText({ key: 'admin.achievements.id' })" :hint="getText({ key: 'admin.achievements.idHint' })" persistent-hint density="compact" class="mb-3" data-test="achievement-id" />
          <VCombobox v-model="editing.icon" :items="PERK_ICON_CHOICES" :label="getText({ key: 'admin.achievements.icon' })" :hint="getText({ key: 'admin.achievements.iconHint' })" persistent-hint density="compact" class="mb-3">
            <template #prepend-inner><GameIcon :name="editing.icon" :size="20" /></template>
            <template #item="{ props: item }">
              <VListItem v-bind="item"><template #prepend><GameIcon :name="item.title" :size="22" class="mr-3" /></template></VListItem>
            </template>
          </VCombobox>
          <div class="rule mb-3">
            <VSelect v-model="editing.stat" :items="achievements.stats.value.map((stat) => ({ title: statLabel(stat), value: stat }))" :label="getText({ key: 'admin.achievements.stat' })" density="compact" hide-details data-test="achievement-stat" />
            <VTextField v-model.number="editing.threshold" type="number" min="1" :label="getText({ key: 'admin.achievements.threshold' })" density="compact" hide-details data-test="achievement-threshold" />
          </div>
          <VSwitch v-model="editing.enabled" :label="getText({ key: 'admin.achievements.enabled' })" density="compact" hide-details class="mb-2" />
          <div v-for="code in LOCALES" :key="code" class="texts mb-2" :data-test="`achievement-texts-${code}`">
            <div class="text-subtitle-2">{{ code.toUpperCase() }}</div>
            <VTextField v-model="editing.texts[code].name" maxlength="40" :label="getText({ key: 'admin.achievements.name' })" density="compact" hide-details class="mb-1" />
            <VTextField v-model="editing.texts[code].description" maxlength="200" :label="getText({ key: 'admin.achievements.description' })" density="compact" hide-details />
          </div>
          <div v-if="errorText" class="text-error mt-2" role="alert" data-test="achievement-form-error">{{ errorText }}</div>
        </VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn @click="editing = null">{{ getText({ key: "buttons.cancel" }) }}</VBtn>
          <VBtn color="primary" :loading="busy" data-test="achievement-save" @click="save">{{ getText({ key: "admin.achievements.save" }) }}</VBtn>
        </VCardActions>
      </VCard>
    </VDialog>

    <VDialog :model-value="confirmingDelete !== null" max-width="420" @update:model-value="confirmingDelete = null">
      <VCard :title="getText({ key: 'admin.delete' })">
        <VCardText>{{ getText({ key: "admin.achievements.confirmDelete" }) }}</VCardText>
        <VCardActions>
          <VSpacer />
          <VBtn @click="confirmingDelete = null">{{ getText({ key: "buttons.cancel" }) }}</VBtn>
          <VBtn color="error" data-test="achievement-confirm-delete" @click="confirmDelete">{{ getText({ key: "admin.delete" }) }}</VBtn>
        </VCardActions>
      </VCard>
    </VDialog>
  </div>
</template>

<style scoped>
.off {
  opacity: 0.55;
}
.id {
  opacity: 0.7;
}
.rule {
  display: grid;
  grid-template-columns: 2fr 1fr;
  gap: 8px;
}
.texts {
  padding: 8px 12px;
  border: 1px solid var(--snake-line, currentColor);
  border-radius: 4px;
}
</style>
