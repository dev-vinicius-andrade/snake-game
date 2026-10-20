<script setup lang="ts">
import { useRouter } from "vue-router";
import { CREDIT_GROUPS, CREDITS } from "@/game/credits";
import { getText } from "@/helpers";

const router = useRouter();
const inGroup = (group: string) => CREDITS.filter((credit) => credit.group === group);
</script>

<template>
  <VRow no-gutters justify="center" class="pa-4">
    <VCol cols="12" md="9" lg="7">
      <VCard :title="getText({ key: 'credits.title' })" :subtitle="getText({ key: 'credits.subtitle' })" data-test="credits-page">
        <VCardText>
          <p class="mb-4" data-test="credits-intro">{{ getText({ key: "credits.intro" }) }}</p>

          <section v-for="group in CREDIT_GROUPS" :key="group" class="mb-5" :data-test="`credits-${group}`">
            <h2 class="text-subtitle-1 mb-2">{{ getText({ key: `credits.groups.${group}` }) }}</h2>
            <ul class="credits">
              <li v-for="credit in inGroup(group)" :key="credit.name" class="credit">
                <a :href="credit.url" target="_blank" rel="noopener noreferrer" class="name">{{ credit.name }}</a>
                <span class="by">{{ credit.by }}</span>
                <a v-if="credit.licenseUrl" :href="credit.licenseUrl" target="_blank" rel="noopener noreferrer" class="license">{{ credit.license }}</a>
                <span v-else class="license">{{ credit.license }}</span>
              </li>
            </ul>
          </section>

          <section class="mb-2" data-test="credits-sounds">
            <h2 class="text-subtitle-1 mb-2">{{ getText({ key: "credits.sounds.title" }) }}</h2>
            <p class="text-body-2">{{ getText({ key: "credits.sounds.text" }) }}</p>
          </section>

          <section data-test="credits-logos">
            <h2 class="text-subtitle-1 mb-2">{{ getText({ key: "credits.logos.title" }) }}</h2>
            <p class="text-body-2">{{ getText({ key: "credits.logos.text" }) }}</p>
          </section>
        </VCardText>
        <VCardActions>
          <VBtn @click="router.push({ name: 'home' })">{{ getText({ key: "account.back" }) }}</VBtn>
        </VCardActions>
      </VCard>
    </VCol>
  </VRow>
</template>

<style scoped>
.credits {
  padding: 0;
  margin: 0;
  list-style: none;
}
.credit {
  display: flex;
  flex-wrap: wrap;
  align-items: baseline;
  gap: 4px 12px;
  padding: 6px 0;
  border-bottom: 1px solid var(--snake-line, rgba(255, 255, 255, 0.12));
}
.name {
  font-weight: 600;
}
.by {
  flex: 1 1 200px;
  opacity: 0.8;
}
.license {
  font-size: 0.8rem;
  opacity: 0.9;
}
</style>
