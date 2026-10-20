<script setup lang="ts">
import { computed, onMounted } from "vue";
import type {
  BountyConfig,
  FruitShowerConfig,
  LightningConfig,
  MeteorConfig,
  MinesConfig,
  SugarRushConfig,
  TurretConfig,
  ZoneConfig,
  useAdminGameplay,
} from "@/composables/useAdminGameplay";
import { getText } from "@/helpers";

const props = defineProps<{ gameplay: ReturnType<typeof useAdminGameplay> }>();
const gameplay = props.gameplay;
const draft = gameplay.draft;

type NumberKeys<T> = { [Key in keyof T]-?: T[Key] extends number ? Key : never }[keyof T];

/** The numbers of each event, in the order they are shown, with the text that labels them. */
const lightningFields: Array<{ key: NumberKeys<LightningConfig>; label: string; step?: number }> = [
  { key: "minIntervalSeconds", label: "lightningMin" },
  { key: "maxIntervalSeconds", label: "lightningMax" },
  { key: "minStrikes", label: "lightningMinStrikes" },
  { key: "maxStrikes", label: "lightningMaxStrikes" },
  { key: "stormSeconds", label: "lightningStormSeconds" },
  { key: "radius", label: "lightningRadius" },
  { key: "telegraphMilliseconds", label: "lightningTelegraph" },
  { key: "aimedShare", label: "lightningAimed", step: 0.05 },
];
const meteorFields: Array<{ key: NumberKeys<MeteorConfig>; label: string; step?: number }> = [
  { key: "minIntervalSeconds", label: "meteorMin" },
  { key: "maxIntervalSeconds", label: "meteorMax" },
  { key: "minMeteors", label: "meteorMinMeteors" },
  { key: "maxMeteors", label: "meteorMaxMeteors" },
  { key: "rainSeconds", label: "meteorRainSeconds" },
  { key: "radius", label: "meteorRadius" },
  { key: "telegraphMilliseconds", label: "meteorTelegraph" },
  { key: "aimedShare", label: "meteorAimed", step: 0.05 },
];

const turretFields: Array<{ key: NumberKeys<TurretConfig>; label: string; step?: number }> = [
  { key: "minIntervalSeconds", label: "turretMin" },
  { key: "maxIntervalSeconds", label: "turretMax" },
  { key: "lifetimeSeconds", label: "turretLifetime" },
  { key: "hitPoints", label: "turretHitPoints" },
  { key: "size", label: "turretSize" },
  { key: "fireIntervalMilliseconds", label: "turretFireInterval" },
  { key: "arms", label: "turretArms" },
  { key: "spinDegrees", label: "turretSpin" },
  { key: "shotSpeed", label: "turretShotSpeed", step: 0.1 },
  { key: "minPlayers", label: "turretMinPlayers" },
  { key: "rewardMass", label: "turretReward" },
  { key: "foodsCredit", label: "turretFoodsCredit" },
];

const zoneFields: Array<{ key: NumberKeys<ZoneConfig>; label: string; step?: number }> = [
  { key: "minIntervalSeconds", label: "zoneMin" },
  { key: "maxIntervalSeconds", label: "zoneMax" },
  { key: "warningSeconds", label: "zoneWarning" },
  { key: "shrinkSeconds", label: "zoneShrink" },
  { key: "holdSeconds", label: "zoneHold" },
  { key: "finalRadius", label: "zoneFinalRadius" },
  { key: "damageIntervalMilliseconds", label: "zoneDamageInterval" },
  { key: "damageMass", label: "zoneDamageMass" },
  { key: "minPlayers", label: "zoneMinPlayers" },
];

const showerFields: Array<{ key: NumberKeys<FruitShowerConfig>; label: string; step?: number }> = [
  { key: "minIntervalSeconds", label: "showerMin" },
  { key: "maxIntervalSeconds", label: "showerMax" },
  { key: "durationSeconds", label: "showerDuration" },
  { key: "fruitsPerSecond", label: "showerFruitsPerSecond" },
  { key: "minPlayers", label: "showerMinPlayers" },
];

const bountyFields: Array<{ key: NumberKeys<BountyConfig>; label: string; step?: number }> = [
  { key: "minIntervalSeconds", label: "bountyMin" },
  { key: "maxIntervalSeconds", label: "bountyMax" },
  { key: "durationSeconds", label: "bountyDuration" },
  { key: "rewardMass", label: "bountyReward" },
  { key: "foodsCredit", label: "bountyFoodsCredit" },
  { key: "minPlayers", label: "bountyMinPlayers" },
];

const minesFields: Array<{ key: NumberKeys<MinesConfig>; label: string; step?: number }> = [
  { key: "minIntervalSeconds", label: "minesMin" },
  { key: "maxIntervalSeconds", label: "minesMax" },
  { key: "minMines", label: "minesMinMines" },
  { key: "maxMines", label: "minesMaxMines" },
  { key: "radius", label: "minesRadius" },
  { key: "triggerRadius", label: "minesTriggerRadius" },
  { key: "armMilliseconds", label: "minesArm" },
  { key: "minPlayers", label: "minesMinPlayers" },
];

const rushFields: Array<{ key: NumberKeys<SugarRushConfig>; label: string; step?: number }> = [
  { key: "minIntervalSeconds", label: "rushMin" },
  { key: "maxIntervalSeconds", label: "rushMax" },
  { key: "durationSeconds", label: "rushDuration" },
  { key: "minPlayers", label: "rushMinPlayers" },
];

/** The perks the turret can give: none, or any perk of the list. */
const perkItems = computed(() => [{ id: "", name: getText({ key: "admin.gameplay.prizeGrantsNone" }) }, ...gameplay.perkChoices.value]);

const KNOWN_ERRORS = ["unavailable", "invalidKey"];
const errorText = computed(() => {
  const error = gameplay.error.value;
  if (!error) return "";
  return KNOWN_ERRORS.includes(error) ? getText({ key: `admin.${error}` }) : error;
});

onMounted(() => {
  if (!gameplay.draft.value) void gameplay.load();
});
</script>

<template>
  <div v-if="draft" class="world-events">
    <VCard variant="outlined" class="mb-4">
      <VCardTitle>{{ getText({ key: "admin.gameplay.worldEventsTitle" }) }}</VCardTitle>
      <VCardText>
        <VSwitch v-model="draft.worldEvents.enabled" color="primary" hide-details :label="getText({ key: 'admin.gameplay.worldEventsOn' })" />

        <div class="event-card" :class="{ 'is-off': !draft.worldEvents.lightning.enabled }" data-event="lightning">
          <div class="event-row">
            <strong>{{ getText({ key: "admin.gameplay.lightningTitle" }) }}</strong>
            <VSwitch v-model="draft.worldEvents.lightning.enabled" class="switch-field" color="primary" hide-details :label="getText({ key: 'admin.gameplay.lightningOn' })" />
          </div>
          <div class="event-help">{{ getText({ key: "admin.gameplay.lightningHelp" }) }}</div>
          <div class="field-grid">
            <VTextField
              v-for="field in lightningFields"
              :key="field.key"
              v-model.number="draft.worldEvents.lightning[field.key]"
              type="number"
              :step="field.step"
              density="compact"
              hide-details
              :label="getText({ key: `admin.gameplay.${field.label}` })"
            />
          </div>
          <VCombobox v-model="draft.worldEvents.lightning.rooms" multiple chips closable-chips density="compact" class="mt-2" hide-details :label="getText({ key: 'admin.gameplay.lightningRooms' })" />
          <VCombobox v-model="draft.worldEvents.lightning.excludedRooms" multiple chips closable-chips density="compact" class="mt-2" hide-details :label="getText({ key: 'admin.gameplay.excludedRooms' })" />
        </div>

        <div class="event-card" :class="{ 'is-off': !draft.worldEvents.meteors.enabled }" data-event="meteors">
          <div class="event-row">
            <strong>{{ getText({ key: "admin.gameplay.meteorTitle" }) }}</strong>
            <VSwitch v-model="draft.worldEvents.meteors.enabled" class="switch-field" color="primary" hide-details :label="getText({ key: 'admin.gameplay.meteorOn' })" />
          </div>
          <div class="event-help">{{ getText({ key: "admin.gameplay.meteorHelp" }) }}</div>
          <div class="field-grid">
            <VTextField
              v-for="field in meteorFields"
              :key="field.key"
              v-model.number="draft.worldEvents.meteors[field.key]"
              type="number"
              :step="field.step"
              density="compact"
              hide-details
              :label="getText({ key: `admin.gameplay.${field.label}` })"
            />
          </div>
          <VCombobox v-model="draft.worldEvents.meteors.rooms" multiple chips closable-chips density="compact" class="mt-2" hide-details :label="getText({ key: 'admin.gameplay.meteorRooms' })" />
          <VCombobox v-model="draft.worldEvents.meteors.excludedRooms" multiple chips closable-chips density="compact" class="mt-2" hide-details :label="getText({ key: 'admin.gameplay.excludedRooms' })" />
        </div>

        <div class="event-card" :class="{ 'is-off': !draft.worldEvents.turret.enabled }" data-event="turret">
          <div class="event-row">
            <strong>{{ getText({ key: "admin.gameplay.turretTitle" }) }}</strong>
            <VSwitch v-model="draft.worldEvents.turret.enabled" class="switch-field" color="primary" hide-details :label="getText({ key: 'admin.gameplay.turretOn' })" />
          </div>
          <div class="event-help">{{ getText({ key: "admin.gameplay.turretHelp" }) }}</div>
          <div class="field-grid">
            <VTextField
              v-for="field in turretFields"
              :key="field.key"
              v-model.number="draft.worldEvents.turret[field.key]"
              type="number"
              :step="field.step"
              density="compact"
              hide-details
              :label="getText({ key: `admin.gameplay.${field.label}` })"
            />
            <VSelect
              v-model="draft.worldEvents.turret.grantsPerk"
              :items="perkItems"
              item-title="name"
              item-value="id"
              density="compact"
              hide-details
              :label="getText({ key: 'admin.gameplay.turretGrantsPerk' })"
            />
          </div>
          <VCombobox v-model="draft.worldEvents.turret.rooms" multiple chips closable-chips density="compact" class="mt-2" hide-details :label="getText({ key: 'admin.gameplay.turretRooms' })" />
          <VCombobox v-model="draft.worldEvents.turret.excludedRooms" multiple chips closable-chips density="compact" class="mt-2" hide-details :label="getText({ key: 'admin.gameplay.excludedRooms' })" />
        </div>

        <div class="event-card" :class="{ 'is-off': !draft.worldEvents.zone.enabled }" data-event="zone">
          <div class="event-row">
            <strong>{{ getText({ key: "admin.gameplay.zoneTitle" }) }}</strong>
            <VSwitch v-model="draft.worldEvents.zone.enabled" class="switch-field" color="primary" hide-details :label="getText({ key: 'admin.gameplay.zoneOn' })" />
          </div>
          <div class="event-help">{{ getText({ key: "admin.gameplay.zoneHelp" }) }}</div>
          <div class="field-grid">
            <VTextField
              v-for="field in zoneFields"
              :key="field.key"
              v-model.number="draft.worldEvents.zone[field.key]"
              type="number"
              :step="field.step"
              density="compact"
              hide-details
              :label="getText({ key: `admin.gameplay.${field.label}` })"
            />
          </div>
          <VCombobox v-model="draft.worldEvents.zone.rooms" multiple chips closable-chips density="compact" class="mt-2" hide-details :label="getText({ key: 'admin.gameplay.zoneRooms' })" />
          <VCombobox v-model="draft.worldEvents.zone.excludedRooms" multiple chips closable-chips density="compact" class="mt-2" hide-details :label="getText({ key: 'admin.gameplay.excludedRooms' })" />
        </div>

        <div class="event-card" :class="{ 'is-off': !draft.worldEvents.fruitShower.enabled }" data-event="fruitShower">
          <div class="event-row">
            <strong>{{ getText({ key: "admin.gameplay.showerTitle" }) }}</strong>
            <VSwitch v-model="draft.worldEvents.fruitShower.enabled" class="switch-field" color="primary" hide-details :label="getText({ key: 'admin.gameplay.showerOn' })" />
          </div>
          <div class="event-help">{{ getText({ key: "admin.gameplay.showerHelp" }) }}</div>
          <div class="field-grid">
            <VTextField
              v-for="field in showerFields"
              :key="field.key"
              v-model.number="draft.worldEvents.fruitShower[field.key]"
              type="number"
              :step="field.step"
              density="compact"
              hide-details
              :label="getText({ key: `admin.gameplay.${field.label}` })"
            />
          </div>
          <VCombobox v-model="draft.worldEvents.fruitShower.rooms" multiple chips closable-chips density="compact" class="mt-2" hide-details :label="getText({ key: 'admin.gameplay.showerRooms' })" />
          <VCombobox v-model="draft.worldEvents.fruitShower.excludedRooms" multiple chips closable-chips density="compact" class="mt-2" hide-details :label="getText({ key: 'admin.gameplay.excludedRooms' })" />
        </div>

        <div class="event-card" :class="{ 'is-off': !draft.worldEvents.bounty.enabled }" data-event="bounty">
          <div class="event-row">
            <strong>{{ getText({ key: "admin.gameplay.bountyTitle" }) }}</strong>
            <VSwitch v-model="draft.worldEvents.bounty.enabled" class="switch-field" color="primary" hide-details :label="getText({ key: 'admin.gameplay.bountyOn' })" />
          </div>
          <div class="event-help">{{ getText({ key: "admin.gameplay.bountyHelp" }) }}</div>
          <div class="field-grid">
            <VTextField
              v-for="field in bountyFields"
              :key="field.key"
              v-model.number="draft.worldEvents.bounty[field.key]"
              type="number"
              :step="field.step"
              density="compact"
              hide-details
              :label="getText({ key: `admin.gameplay.${field.label}` })"
            />
          </div>
          <VCombobox v-model="draft.worldEvents.bounty.rooms" multiple chips closable-chips density="compact" class="mt-2" hide-details :label="getText({ key: 'admin.gameplay.bountyRooms' })" />
          <VCombobox v-model="draft.worldEvents.bounty.excludedRooms" multiple chips closable-chips density="compact" class="mt-2" hide-details :label="getText({ key: 'admin.gameplay.excludedRooms' })" />
        </div>

        <div class="event-card" :class="{ 'is-off': !draft.worldEvents.mines.enabled }" data-event="mines">
          <div class="event-row">
            <strong>{{ getText({ key: "admin.gameplay.minesTitle" }) }}</strong>
            <VSwitch v-model="draft.worldEvents.mines.enabled" class="switch-field" color="primary" hide-details :label="getText({ key: 'admin.gameplay.minesOn' })" />
          </div>
          <div class="event-help">{{ getText({ key: "admin.gameplay.minesHelp" }) }}</div>
          <div class="field-grid">
            <VTextField
              v-for="field in minesFields"
              :key="field.key"
              v-model.number="draft.worldEvents.mines[field.key]"
              type="number"
              :step="field.step"
              density="compact"
              hide-details
              :label="getText({ key: `admin.gameplay.${field.label}` })"
            />
          </div>
          <VCombobox v-model="draft.worldEvents.mines.rooms" multiple chips closable-chips density="compact" class="mt-2" hide-details :label="getText({ key: 'admin.gameplay.minesRooms' })" />
          <VCombobox v-model="draft.worldEvents.mines.excludedRooms" multiple chips closable-chips density="compact" class="mt-2" hide-details :label="getText({ key: 'admin.gameplay.excludedRooms' })" />
        </div>

        <div class="event-card" :class="{ 'is-off': !draft.worldEvents.sugarRush.enabled }" data-event="sugarRush">
          <div class="event-row">
            <strong>{{ getText({ key: "admin.gameplay.rushTitle" }) }}</strong>
            <VSwitch v-model="draft.worldEvents.sugarRush.enabled" class="switch-field" color="primary" hide-details :label="getText({ key: 'admin.gameplay.rushOn' })" />
          </div>
          <div class="event-help">{{ getText({ key: "admin.gameplay.rushHelp" }) }}</div>
          <div class="field-grid">
            <VTextField
              v-for="field in rushFields"
              :key="field.key"
              v-model.number="draft.worldEvents.sugarRush[field.key]"
              type="number"
              :step="field.step"
              density="compact"
              hide-details
              :label="getText({ key: `admin.gameplay.${field.label}` })"
            />
          </div>
          <VCombobox v-model="draft.worldEvents.sugarRush.rooms" multiple chips closable-chips density="compact" class="mt-2" hide-details :label="getText({ key: 'admin.gameplay.rushRooms' })" />
          <VCombobox v-model="draft.worldEvents.sugarRush.excludedRooms" multiple chips closable-chips density="compact" class="mt-2" hide-details :label="getText({ key: 'admin.gameplay.excludedRooms' })" />
        </div>
      </VCardText>
    </VCard>
  </div>
  <div v-else-if="errorText" class="text-error" role="alert">{{ errorText }}</div>
</template>

<style scoped>
/* One card per event. Inside it the fields sit side by side and wrap onto more lines as the screen narrows. */
.event-card {
  margin-top: 14px;
  padding: 10px 12px 12px;
  border: 1px solid var(--snake-line);
}
.event-card.is-off {
  opacity: 0.55;
}
.event-row {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  margin-top: 4px;
}
.event-row > :first-child {
  flex: 1 1 130px;
}
.event-help {
  margin: 2px 0 6px;
  font-size: 12px;
  opacity: 0.65;
}
.field-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 8px;
  align-items: center;
  margin-top: 8px;
}
.field-grid > * {
  min-width: 0;
}
.switch-field {
  flex: 0 0 auto !important;
}
</style>
