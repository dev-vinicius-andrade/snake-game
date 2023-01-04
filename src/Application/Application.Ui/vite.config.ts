// Plugins
import vue from "@vitejs/plugin-vue";
import vuetify, { transformAssetUrls } from "vite-plugin-vuetify";
import VueI18nPlugin from "@intlify/unplugin-vue-i18n/vite";
import Icons from "unplugin-icons/vite";
import vueJsx from "@vitejs/plugin-vue-jsx";
import AutoImport from "unplugin-auto-import/vite";
import Components from "unplugin-vue-components/vite";
import DefineOptions from "unplugin-vue-define-options/vite";
import { defineConfig } from "vite";
import { fileURLToPath, URL } from "node:url";

// https://vitejs.dev/config/
export default defineConfig({
  server: {
    port: 3000,
  },
  build: {
    chunkSizeWarningLimit: 5000,
    minify: false,
  },
  plugins: [
    Icons({ compiler: "vue3" }),
    vue({
      template: { transformAssetUrls },
    }),
    vueJsx(),
    // https://github.com/vuetifyjs/vuetify-loader/tree/next/packages/vite-plugin
    vuetify({
      autoImport: true,
      styles: {
        configFile: "src/styles/variables/vuetify.scss",
      },
    }),
    Components({
      dirs: ["src/components"],
      dts: true,
    }),
    AutoImport({
      include: [
        /\.[tj]sx?$/, // .ts, .tsx, .js, .jsx
        /\.vue$/,
        /\.vue\?vue/, // .vue
        /\.md$/, // .md
      ],
      imports: ["vue", "vue-router", "@vueuse/core", "vue-i18n", "pinia"],
      dirs: [
        "src/store",
        "src/helpers",
        "src/plugins/utils",
        "src/enums",
        "src/composables",
        "src/types/*/index.ts",
      ],
      vueTemplate: true,
      dts: true,
    }),
    VueI18nPlugin({
      runtimeOnly: false,
      compositionOnly: true,
      include: [
        fileURLToPath(
          new URL("./src/plugins/i18n/locales/**", import.meta.url)
        ),
      ],
    }),
    DefineOptions(),
  ],
  define: { "process.env": {} },
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
      "@assets": fileURLToPath(new URL("./src/assets", import.meta.url)),
      "@axios": fileURLToPath(new URL("./src/plugins/axios", import.meta.url)),
      "@components": fileURLToPath(
        new URL("./src/components", import.meta.url)
      ),
      "@enums": fileURLToPath(new URL("./src/enums", import.meta.url)),
      "@router": fileURLToPath(new URL("./src/router", import.meta.url)),
      "@locales": fileURLToPath(new URL("./src/locales", import.meta.url)),
      "@styles": fileURLToPath(new URL("./src/styles", import.meta.url)),
      "@mixins": fileURLToPath(new URL("./src/mixins", import.meta.url)),
      "@types": fileURLToPath(new URL("./src/types", import.meta.url)),
      "@views": fileURLToPath(new URL("./src/views", import.meta.url)),
      "@plugins": fileURLToPath(new URL("./src/plugins", import.meta.url)),
      "@store": fileURLToPath(new URL("./src/store", import.meta.url)),
      "@models": fileURLToPath(new URL("./src/models", import.meta.url)),
      "@interfaces": fileURLToPath(
        new URL("./src/interfaces", import.meta.url)
      ),
      "@composables": fileURLToPath(
        new URL("./src/composables", import.meta.url)
      ),
      "@layouts": fileURLToPath(new URL("./src/layouts", import.meta.url)),
      "@helpers": fileURLToPath(new URL("./src/helpers", import.meta.url)),
    },
    extensions: [".js", ".json", ".jsx", ".mjs", ".ts", ".tsx", ".vue"],
  },
});
