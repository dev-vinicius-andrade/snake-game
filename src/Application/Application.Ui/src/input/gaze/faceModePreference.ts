import type { StorageLike } from "./calibrationStorage";
import type { FaceMode } from "./mediapipeGazeSource";

export interface FaceModePreference {
  load(): FaceMode | null;
  save(mode: FaceMode): void;
}

export const FACE_MODE_KEY = "snake.faceMode";

/** Remembers whether the player controls with the eyes or the head. Storage can fail, so every call may fail quietly. */
export function createFaceModePreference(
  storage: StorageLike | null = typeof localStorage === "undefined" ? null : localStorage,
  key: string = FACE_MODE_KEY
): FaceModePreference {
  return {
    load() {
      try {
        const stored = storage?.getItem(key);
        return stored === "eyes" || stored === "head" ? stored : null;
      } catch {
        return null;
      }
    },
    save(mode) {
      try {
        storage?.setItem(key, mode);
      } catch {
        // Forgetting the choice only means picking the mode again next time.
      }
    },
  };
}
