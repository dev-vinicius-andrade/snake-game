<script setup lang="ts">
import { useRouter } from "vue-router";
import ProfileStats from "@/components/account/ProfileStats.vue";
import { getText } from "@/helpers";

const router = useRouter();
const toAccount = () => router.replace({ name: "account" });
</script>

<template>
  <VRow no-gutters justify="center" class="pa-4">
    <VCol cols="12" md="10" lg="8">
      <VCard :title="getText({ key: 'profile.title' })">
        <VCardText>
          <ProfileStats @no-account="toAccount" @sign-in="toAccount" />
        </VCardText>
        <VCardActions>
          <VBtn @click="router.push({ name: 'account' })">{{ getText({ key: "account.title" }) }}</VBtn>
          <VBtn @click="router.push({ name: 'home' })">{{ getText({ key: "account.back" }) }}</VBtn>
        </VCardActions>
      </VCard>
    </VCol>
  </VRow>
</template>
