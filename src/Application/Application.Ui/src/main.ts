import { createApp } from "vue";
import { createPinia } from "pinia";
import { loadFonts } from "@plugins/webfontloader";
import customDirectives from "@plugins/customDirectives";
import i18n from "@plugins/i18n";
import router from "@router";
import App from "./App.vue";
import vuetify from "@/plugins/vuetify";
import "@/styles/styles.scss";

async function main() {
  loadFonts();
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
