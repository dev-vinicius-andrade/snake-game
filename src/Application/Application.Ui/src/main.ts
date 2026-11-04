import { createApp } from "vue";
import { createPinia } from "pinia";
import { loadFonts } from "@plugins/webfontloader";
import customDirectives from "@plugins/customDirectives";
import i18n from "@plugins/i18n";
import { applyInitialLocale } from "@/composables/useLocale";
import { currentPerfMode } from "@/render/perfMode";
import router from "@router";
import App from "./App.vue";
import vuetify from "@/plugins/vuetify";
import "@/styles/styles.scss";

async function main() {
  // `?perf` is in the address of the page that was opened: read it before the router moves on and drops it.
  currentPerfMode();
  loadFonts();
  applyInitialLocale();
  const pinia = createPinia();
  createApp(App)
    .use(pinia)
    .use(customDirectives)
    .use(router)
    .use(i18n)
    .use(vuetify)
    .mount("#app");
}
main();
