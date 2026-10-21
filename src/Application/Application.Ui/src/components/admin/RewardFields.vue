<script setup lang="ts">
import { computed } from "vue";
import type { RewardConfig } from "@/composables/useAdminGameplay";
import { getText } from "@/helpers";

/** The fields of one reward of the catalog: what it gives besides mass and foods. Everything off gives nothing. */
const props = defineProps<{
  /** The reward being edited; its fields change in place. */
  reward: RewardConfig;
  /** The perks of the list, for the perk a tier surge lifts. */
  perks: Array<{ id: string; name: string }>;
}>();

const surgePerks = computed(() => [{ id: "", name: getText({ key: "admin.gameplay.rewardSurgeRandom" }) }, ...props.perks]);
</script>

<template>
  <div class="reward" data-reward>
    <div class="reward-group">
      <VTextField v-model.number="reward.bonusMass" type="number" min="0" max="10000" density="compact" hide-details :label="getText({ key: 'admin.gameplay.rewardBonusMass' })" />
      <VTextField v-model.number="reward.extraLives" type="number" min="0" max="5" density="compact" hide-details :label="getText({ key: 'admin.gameplay.rewardLives' })" />
      <div class="reward-switches">
        <VSwitch v-model="reward.grantsRandomPerk" color="primary" density="compact" hide-details :label="getText({ key: 'admin.gameplay.rewardRandomPerk' })" />
        <VSwitch v-model="reward.mend" color="primary" density="compact" hide-details :label="getText({ key: 'admin.gameplay.rewardMend' })" />
      </div>
    </div>
    <div class="reward-surge" data-surge>
      <div class="reward-surge-title">{{ getText({ key: "admin.gameplay.rewardSurge" }) }}</div>
      <div class="reward-group">
        <VTextField v-model.number="reward.tierSurgeMin" type="number" min="0" max="10" density="compact" hide-details :label="getText({ key: 'admin.gameplay.rewardSurgeMin' })" />
        <VTextField v-model.number="reward.tierSurgeMax" type="number" min="0" max="10" density="compact" hide-details :label="getText({ key: 'admin.gameplay.rewardSurgeMax' })" />
        <VSelect
          v-model="reward.tierSurgePerk"
          :items="surgePerks"
          item-title="name"
          item-value="id"
          density="compact"
          hide-details
          :label="getText({ key: 'admin.gameplay.rewardSurgePerk' })"
        />
      </div>
    </div>
  </div>
</template>

<style scoped>
.reward-group {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 12px;
  align-items: center;
}
.reward-group > * {
  min-width: 0;
}
.reward-switches {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 16px;
}
.reward-surge {
  margin-top: 12px;
  padding: 8px 10px 10px;
  border: 1px dashed var(--snake-line);
}
.reward-surge-title {
  margin-bottom: 8px;
  font-size: 13px;
  font-weight: bold;
}
</style>
