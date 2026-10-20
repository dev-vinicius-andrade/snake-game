<script setup lang="ts">
import { computed } from "vue";
import type { RewardConfig } from "@/composables/useAdminGameplay";
import { getText } from "@/helpers";

/** The fields of a reward: what else a prize, the turret, a bounty or the maze prize gives. Everything off gives nothing. */
const props = defineProps<{
  /** The reward being edited; its fields change in place. */
  reward: RewardConfig;
  /** The perks of the list, for the perk a tier surge lifts. */
  perks: Array<{ id: string; name: string }>;
}>();

const surgePerks = computed(() => [{ id: "", name: getText({ key: "admin.gameplay.rewardSurgeRandom" }) }, ...props.perks]);
</script>

<template>
  <fieldset class="reward" data-reward>
    <legend>{{ getText({ key: "admin.gameplay.rewardTitle" }) }}</legend>
    <div class="reward-help">{{ getText({ key: "admin.gameplay.rewardHelp" }) }}</div>
    <div class="reward-grid">
      <VTextField v-model.number="reward.bonusMass" type="number" min="0" max="10000" density="compact" hide-details :label="getText({ key: 'admin.gameplay.rewardBonusMass' })" />
      <VTextField v-model.number="reward.extraLives" type="number" min="0" max="5" density="compact" hide-details :label="getText({ key: 'admin.gameplay.rewardLives' })" />
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
      <VSwitch v-model="reward.grantsRandomPerk" color="primary" density="compact" hide-details :label="getText({ key: 'admin.gameplay.rewardRandomPerk' })" />
      <VSwitch v-model="reward.mend" color="primary" density="compact" hide-details :label="getText({ key: 'admin.gameplay.rewardMend' })" />
    </div>
  </fieldset>
</template>

<style scoped>
.reward {
  margin-top: 10px;
  padding: 6px 10px 10px;
  border: 1px dashed var(--snake-line);
}
.reward legend {
  padding: 0 6px;
  font-size: 13px;
  font-weight: bold;
}
.reward-help {
  margin-bottom: 6px;
  font-size: 12px;
  opacity: 0.75;
}
.reward-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
  gap: 8px;
  align-items: center;
}
</style>
