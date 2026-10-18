<script setup lang="ts">
import { RouterLink } from "vue-router";
import SnakeLogo from "@/components/brand/SnakeLogo.vue";
import LanguageSwitcher from "@/components/brand/LanguageSwitcher.vue";
import SiteFooter from "@/components/brand/SiteFooter.vue";
import { REPOSITORY_URL } from "@/config/links";
import { getText } from "@/helpers";

const t = (key: string) => getText({ key: `about.${key}` });

/** The pieces of the system, in the order a player meets them. */
const COMPONENTS = ["site", "manager", "game", "broker", "cache", "tunnel"] as const;
const FLOW = ["f1", "f2", "f3", "f4", "f5", "f6"] as const;
const CONTROLS = ["keyboard", "mouse", "head", "eyes"] as const;
const RULES = ["r1", "r2", "r3", "r4"] as const;
</script>

<template>
  <main class="about">
    <LanguageSwitcher />
    <RouterLink :to="{ name: 'home' }" class="back">&larr; {{ t("back") }}</RouterLink>
    <SnakeLogo class="logo" />
    <p class="lead">{{ t("lead") }}</p>

    <section class="block">
      <h2 class="pixel-font">{{ t("rulesTitle") }}</h2>
      <ul class="list">
        <li v-for="rule in RULES" :key="rule">{{ t(rule) }}</li>
      </ul>
    </section>

    <section class="block">
      <h2 class="pixel-font">{{ t("controlsTitle") }}</h2>
      <div class="grid">
        <div v-for="control in CONTROLS" :key="control" class="card">
          <h3>{{ t(`${control}Title`) }}</h3>
          <p>{{ t(`${control}Text`) }}</p>
        </div>
      </div>
      <p class="note">{{ t("privacy") }}</p>
    </section>

    <section class="block">
      <h2 class="pixel-font">{{ t("archTitle") }}</h2>
      <p>{{ t("archIntro") }}</p>
      <div class="grid parts">
        <div v-for="part in COMPONENTS" :key="part" class="card" :class="`part-${part}`">
          <h3>{{ t(`${part}Title`) }}</h3>
          <p class="stack">{{ t(`${part}Stack`) }}</p>
          <p>{{ t(`${part}Text`) }}</p>
        </div>
      </div>
    </section>

    <section class="block">
      <h2 class="pixel-font">{{ t("flowTitle") }}</h2>
      <ol class="flow">
        <li v-for="step in FLOW" :key="step">{{ t(step) }}</li>
      </ol>
    </section>

    <section class="block">
      <h2 class="pixel-font">{{ t("deployTitle") }}</h2>
      <p>{{ t("deployText") }}</p>
      <p>
        <a class="repo" :href="REPOSITORY_URL" target="_blank" rel="noopener noreferrer">
          <VIcon class="mr-2">mdi-github</VIcon>{{ t("repo") }}
        </a>
      </p>
    </section>

    <SiteFooter />
  </main>
</template>

<style scoped>
.about {
  display: flex;
  flex-direction: column;
  gap: 20px;
  max-width: 880px;
  min-height: 100vh;
  min-height: 100dvh;
  margin: 0 auto;
  padding: max(20px, env(safe-area-inset-top)) 16px max(16px, env(safe-area-inset-bottom));
  box-sizing: border-box;
  line-height: 1.6;
}
.back {
  align-self: flex-start;
  color: var(--snake-muted);
  text-decoration: none;
}
.back:hover {
  color: var(--snake-green);
}
.logo {
  align-self: center;
}
.lead {
  margin: 0;
  color: var(--snake-text);
  font-size: 1.05rem;
  text-align: center;
}
.block {
  display: flex;
  flex-direction: column;
  gap: 12px;
  padding: 16px;
  background: var(--snake-surface);
  border: 3px solid var(--snake-line);
  box-shadow: 4px 4px 0 rgba(0, 0, 0, 0.55);
}
.block h2 {
  margin: 0;
  color: var(--snake-green);
  font-size: 0.85rem;
  line-height: 1.6;
}
.block p {
  margin: 0;
}
.list {
  margin: 0;
  padding-left: 20px;
}
.grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
  gap: 12px;
}
.card {
  padding: 12px 14px;
  background: var(--snake-surface-2);
  border: 2px solid var(--snake-line);
}
.card h3 {
  margin: 0 0 4px;
  font-size: 1rem;
}
.card p {
  margin: 0;
  color: var(--snake-text);
  font-size: 0.92rem;
}
.card .stack {
  margin-bottom: 6px;
  color: var(--snake-muted);
  font-family: ui-monospace, "Cascadia Mono", Consolas, monospace;
  font-size: 0.8rem;
}
.part-site {
  border-left-color: #42d392;
}
.part-manager,
.part-game {
  border-left-color: #b573e0;
}
.part-broker {
  border-left-color: #fbbf24;
}
.part-cache {
  border-left-color: #f87171;
}
.part-tunnel {
  border-left-color: #60a5fa;
}
.part-site,
.part-manager,
.part-game,
.part-broker,
.part-cache,
.part-tunnel {
  border-left-width: 6px;
}
.note {
  color: var(--snake-muted);
  font-size: 0.88rem;
}
.flow {
  display: flex;
  flex-direction: column;
  gap: 8px;
  margin: 0;
  padding-left: 22px;
}
.flow li::marker {
  color: var(--snake-green);
  font-weight: 700;
}
.repo {
  display: inline-flex;
  align-items: center;
  min-height: 44px;
  padding: 0 14px;
  color: var(--snake-green);
  text-decoration: none;
  border: 2px solid var(--snake-green);
}
.repo:hover,
.repo:focus-visible {
  color: #052e16;
  background: var(--snake-green);
}
</style>
