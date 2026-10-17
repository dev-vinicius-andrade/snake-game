/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** Base url of the manager api, for example http://localhost:60510 */
  readonly VITE_MANAGER_URL?: string
  /** Face landmarker model for camera gaze. Defaults to the official MediaPipe download. */
  readonly VITE_FACE_LANDMARKER_MODEL_URL?: string
  /** MediaPipe wasm folder for camera gaze. Defaults to jsDelivr, same version as the npm package. */
  readonly VITE_MEDIAPIPE_WASM_URL?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}

declare module '*.vue' {
  import type { DefineComponent } from 'vue'
  const component: DefineComponent<{}, {}, any>
  export default component
}
