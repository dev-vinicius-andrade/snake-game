<script setup lang="ts">
import { computed } from "vue";
import { ISSUES_URL } from "@/config/links";
import { getText } from "@/helpers";

/** The terms of service and the privacy policy: public pages, the addresses a provider (Discord, say) asks for. */
const props = defineProps<{ page: "terms" | "privacy" }>();

const SECTIONS = { terms: 7, privacy: 9 } as const;

const sections = computed(() =>
  Array.from({ length: SECTIONS[props.page] }, (_, index) => {
    const key = `legal.${props.page}Page.s${index + 1}`;
    return {
      id: `s${index + 1}`,
      title: getText({ key: `${key}.title` }),
      body: getText({ key: `${key}.body`, params: { issues: ISSUES_URL } }),
    };
  })
);
</script>

<template>
  <VRow no-gutters justify="center" class="pa-4">
    <VCol cols="12" md="8" lg="6">
      <VCard :title="getText({ key: `legal.${page}` })" :subtitle="getText({ key: 'legal.updated' })" data-test="legal-page" :data-page="page">
        <VCardText>
          <section v-for="section in sections" :key="section.id" class="mb-4" :data-test="`legal-${section.id}`">
            <h2 class="text-subtitle-1 mb-1">{{ section.title }}</h2>
            <p class="text-body-2">{{ section.body }}</p>
          </section>
        </VCardText>
        <VCardActions>
          <VBtn :to="{ name: 'home' }">{{ getText({ key: "legal.back" }) }}</VBtn>
          <VBtn variant="text" :to="{ name: page === 'terms' ? 'privacy' : 'terms' }" data-test="legal-other">
            {{ getText({ key: page === "terms" ? "legal.privacy" : "legal.terms" }) }}
          </VBtn>
        </VCardActions>
      </VCard>
    </VCol>
  </VRow>
</template>
