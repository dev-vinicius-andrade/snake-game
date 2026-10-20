<script setup lang="ts">
import { computed, onMounted } from "vue";
import GameIcon from "@/components/game/GameIcon.vue";
import { tokenRejected } from "@/composables/useAccount";
import { useLocale } from "@/composables/useLocale";
import { useProfile } from "@/composables/useProfile";
import { STAT_KEYS, TIME_STATS, achievementText, buildAchievementViews, formatPlayTime, mergeAchievementTexts, sortAchievements } from "@/game/achievements";
import { ACHIEVEMENT_TEXTS } from "@/game/achievementTexts";
import { getText } from "@/helpers";

const { locale } = useLocale();
const profile = useProfile();

const stats = computed(() => profile.profile.value?.stats ?? {});
const views = computed(() =>
  sortAchievements(buildAchievementViews(profile.definitions.value, profile.profile.value?.achievements ?? [], stats.value))
);
const unlockedCount = computed(() => views.value.filter((view) => view.unlocked).length);

function statValue(key: string): string {
  const value = stats.value[key] ?? 0;
  if (!TIME_STATS.includes(key)) return String(value);
  const { hours, minutes, seconds } = formatPlayTime(value);
  if (hours > 0) return getText({ key: "profile.time.hoursMinutes", params: { hours, minutes } });
  if (minutes > 0) return getText({ key: "profile.time.minutes", params: { minutes } });
  return getText({ key: "profile.time.seconds", params: { seconds } });
}

const texts = computed(() => mergeAchievementTexts(ACHIEVEMENT_TEXTS, profile.definitions.value));
function name(id: string) {
  return achievementText(texts.value, locale.value, id).name;
}
function description(id: string) {
  return achievementText(texts.value, locale.value, id).description;
}

/** Signed out with no token at all (not one the manager refused): there is nothing to show, and the page decides where the player goes. */
const emit = defineEmits<{ (event: "no-account"): void; (event: "sign-in"): void }>();
const signedOut = computed(() => profile.status.value === "signedOut");

onMounted(async () => {
  await profile.load();
  if (signedOut.value && !tokenRejected.value) emit("no-account");
});
</script>

<template>
  <div data-test="profile-stats">
    <div v-if="profile.profile.value?.account.display" class="text-caption mb-2" data-test="profile-display">{{ profile.profile.value.account.display }}</div>
  <div v-if="profile.status.value === 'loading' || profile.status.value === 'idle'" role="status">
    {{ getText({ key: "profile.loading" }) }}
  </div>

  <template v-else-if="signedOut">
    <VAlert type="warning" variant="tonal" role="alert" data-test="profile-signed-out">
      {{ getText({ key: "profile.sessionEnded" }) }}
    </VAlert>
    <VBtn class="mt-4" color="primary" @click="emit('sign-in')">{{ getText({ key: "account.title" }) }}</VBtn>
  </template>

  <template v-else-if="profile.status.value === 'error'">
    <VAlert type="error" variant="tonal" role="alert" data-test="profile-error">{{ getText({ key: "profile.error" }) }}</VAlert>
    <VBtn class="mt-4" @click="profile.load()">{{ getText({ key: "profile.retry" }) }}</VBtn>
  </template>

  <template v-else>
    <h2 class="text-h6 mb-2">{{ getText({ key: "profile.statsTitle" }) }}</h2>
    <dl class="stats" data-test="stats">
      <div v-for="key in STAT_KEYS" :key="key" class="stat">
        <dt>{{ getText({ key: `profile.stats.${key}` }) }}</dt>
        <dd :data-test="`stat-${key}`">{{ statValue(key) }}</dd>
      </div>
    </dl>

    <h2 class="text-h6 mt-6 mb-1">{{ getText({ key: "profile.achievementsTitle" }) }}</h2>
    <p class="text-caption mb-2">
      {{ getText({ key: "profile.achievementsCount", params: { unlocked: unlockedCount, total: views.length } }) }}
    </p>
    <ul class="achievements" data-test="achievements">
      <li v-for="view in views" :key="view.id" class="achievement" :class="{ locked: !view.unlocked }" :data-test="`achievement-${view.id}`">
        <GameIcon :name="view.icon" :size="36" />
        <div class="body">
          <div class="title">
            {{ name(view.id) }}
            <span class="state">{{ getText({ key: view.unlocked ? "profile.stateUnlocked" : "profile.stateLocked" }) }}</span>
          </div>
          <div class="text-caption">{{ description(view.id) }}</div>
          <VProgressLinear
            :model-value="view.percent"
            :color="view.unlocked ? 'success' : 'primary'"
            height="6"
            class="my-1"
            :aria-label="name(view.id)"
          />
          <div class="text-caption">
            {{ getText({ key: "profile.progress", params: { value: Math.min(view.value, view.threshold), threshold: view.threshold } }) }}
          </div>
        </div>
      </li>
    </ul>
  </template>
  </div>
</template>

<style scoped>
.stats {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
  gap: 8px;
  margin: 0;
}
.stat {
  padding: 8px 12px;
  border: 1px solid currentColor;
  border-radius: 4px;
}
.stat dt {
  font-size: 0.8rem;
  opacity: 0.75;
}
.stat dd {
  margin: 0;
  font-size: 1.25rem;
}
.achievements {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 8px;
  padding: 0;
  margin: 0;
  list-style: none;
}
.achievement {
  display: flex;
  gap: 12px;
  padding: 10px 12px;
  border: 1px solid currentColor;
  border-radius: 4px;
}
.achievement.locked {
  opacity: 0.6;
}
.body {
  flex: 1;
  min-width: 0;
}
.title {
  display: flex;
  justify-content: space-between;
  gap: 8px;
}
.state {
  font-size: 0.7rem;
  text-transform: uppercase;
  opacity: 0.8;
}
</style>
