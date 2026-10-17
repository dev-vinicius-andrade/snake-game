/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Base url of the manager api, for example http://localhost:60510 */
  readonly VITE_MANAGER_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}

declare module '*.vue' {
  import type { DefineComponent } from 'vue'
  const component: DefineComponent<{}, {}, any>
  export default component
}
