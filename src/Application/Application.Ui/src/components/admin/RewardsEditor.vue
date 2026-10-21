<script setup lang="ts">
import type { useAdminGameplay } from "@/composables/useAdminGameplay";
import { getText } from "@/helpers";
import RewardFields from "./RewardFields.vue";

/** The reward catalog: rewards are made, named and deleted here. Prizes, the turret, the bounty and the maze pick from it. */
const props = defineProps<{ gameplay: ReturnType<typeof useAdminGameplay> }>();
const gameplay = props.gameplay;
const draft = gameplay.draft;

/** Prizes and events that pick a reward, so deleting it can say what it takes away from. */
function usedBy(id: string): string[] {
  const config = draft.value;
  if (!config) return [];
  const prizes = config.prize.prizes.filter((prize) => prize.rewardIds.includes(id)).map((prize) => prize.name || prize.id);
  const events = (["turret", "bounty", "maze"] as const).filter((key) => config.worldEvents[key].rewardIds.includes(id)).map((key) => getText({ key: `admin.gameplay.rewardUsedBy.${key}` }));
  return [...prizes, ...events];
}
</script>

<template>
  <VCard v-if="draft" variant="outlined" class="mb-4">
    <VCardTitle>{{ getText({ key: "admin.gameplay.rewardsTitle" }) }}</VCardTitle>
    <VCardText>
      <p class="rewards-help">{{ getText({ key: "admin.gameplay.rewardsHelp" }) }}</p>
      <div v-if="draft.rewards.length === 0" class="rewards-empty" data-rewards-empty>{{ getText({ key: "admin.gameplay.rewardsEmpty" }) }}</div>

      <div v-for="(entry, index) in draft.rewards" :key="index" class="reward-card" data-reward-card>
        <div class="reward-head">
          <VTextField v-model="entry.name" density="compact" hide-details maxlength="64" :label="getText({ key: 'admin.gameplay.rewardName' })" />
          <VBtn size="small" variant="text" color="error" data-reward-remove @click="gameplay.removeReward(index)">{{ getText({ key: "admin.gameplay.rewardRemove" }) }}</VBtn>
        </div>
        <RewardFields :reward="entry.reward" :perks="gameplay.perkChoices.value" />
        <div class="reward-used">
          {{ usedBy(entry.id).length > 0 ? getText({ key: "admin.gameplay.rewardUsed", params: { by: usedBy(entry.id).join(", ") } }) : getText({ key: "admin.gameplay.rewardUnused" }) }}
        </div>
      </div>

      <VBtn class="mt-3" size="small" data-reward-add @click="gameplay.addReward">{{ getText({ key: "admin.gameplay.rewardAdd" }) }}</VBtn>
    </VCardText>
  </VCard>
</template>

<style scoped>
.rewards-help,
.rewards-empty {
  margin: 0 0 8px;
  font-size: 13px;
  opacity: 0.75;
}
.reward-card {
  margin-top: 14px;
  padding: 10px 12px 12px;
  border: 1px solid var(--snake-line);
}
.reward-head {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  margin-bottom: 12px;
}
.reward-head > * {
  flex: 1 1 180px;
  min-width: 0;
}
.reward-head > :last-child {
  flex: 0 0 auto;
}
.reward-used {
  margin-top: 8px;
  font-size: 12px;
  opacity: 0.65;
}
</style>
