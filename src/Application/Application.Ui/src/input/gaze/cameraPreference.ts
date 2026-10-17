import type { StorageLike } from "./calibrationStorage";

export interface CameraPreferenceStorage {
  load(): string | null;
  /** Null forgets the choice, so the browser default is used again. */
  save(cameraId: string | null): void;
}

export const CAMERA_PREFERENCE_KEY = "snake.cameraId";

/** Remembers which camera the player picked. Storage can be blocked or missing, so every call may fail quietly. */
export function createCameraPreferenceStorage(
  storage: StorageLike | null = typeof localStorage === "undefined" ? null : localStorage,
  key: string = CAMERA_PREFERENCE_KEY
): CameraPreferenceStorage {
  return {
    load() {
      try {
        return storage?.getItem(key) || null;
      } catch {
        return null;
      }
    },
    save(cameraId) {
      try {
        if (cameraId) storage?.setItem(key, cameraId);
        else storage?.removeItem(key);
      } catch {
        // Forgetting the choice only means picking the camera again next time.
      }
    },
  };
}
