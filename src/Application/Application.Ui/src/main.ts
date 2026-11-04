import { createApp, type App as VueApp } from "vue";
import { createPinia } from "pinia";
import { loadFonts } from "@plugins/webfontloader";
import customDirectives from "@plugins/customDirectives";
import i18n from "@plugins/i18n";
import { applyInitialLocale } from "@/composables/useLocale";
import router from "@router";
import App from "./App.vue";
import vuetify from "@/plugins/vuetify";
import "@/styles/styles.scss";

/** With `?perf`: tells the frame meter how long each component's updates take, so the overlay can name the one that is slow. */
function measureComponentUpdates(app: VueApp) {
  const started = new WeakMap<object, number>();
  app.mixin({
    beforeUpdate() {
      started.set(this, performance.now());
    },
    updated() {
      const at = started.get(this);
      if (at === undefined) return;
      const now = performance.now();
      const name = this.$options.__name ?? this.$options.name ?? "?";
      (window as unknown as { __snakePerf?: { component(at: number, name: string, ms: number): void } }).__snakePerf?.component(now, name, now - at);
    },
  });
}

async function main() {
  loadFonts();
  applyInitialLocale();
  const pinia = createPinia();
  const app = createApp(App);
  if (new URLSearchParams(window.location.search).has("perf")) measureComponentUpdates(app);
  app
    .use(pinia)
    .use(customDirectives)
    .use(router)
    .use(i18n)
    .use(vuetify)
    .mount("#app");
}
main();
