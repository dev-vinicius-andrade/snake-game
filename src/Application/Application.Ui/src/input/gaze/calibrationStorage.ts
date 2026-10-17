import { parseCalibration, type Calibration } from "./calibration";

/** What the calibration store needs from `localStorage`. */
export interface StorageLike {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

export interface CalibrationStorage {
  load(): Calibration | null;
  save(calibration: Calibration): void;
  clear(): void;
}

export const CALIBRATION_STORAGE_KEY = "snake.gazeCalibration";
export const HEAD_CALIBRATION_KEY = "snake.headCalibration";

/** Keeps the eye calibration between visits. Storage can be blocked or missing, so every call is allowed to fail. */
export function createCalibrationStorage(
  storage: StorageLike | null = typeof localStorage === "undefined" ? null : localStorage,
  key: string = CALIBRATION_STORAGE_KEY
): CalibrationStorage {
  return {
    load() {
      try {
        const stored = storage?.getItem(key);
        return stored ? parseCalibration(JSON.parse(stored)) : null;
      } catch {
        return null;
      }
    },
    save(calibration) {
      try {
        storage?.setItem(key, JSON.stringify(calibration));
      } catch {
        // Not being able to remember the calibration only costs a recalibration next time.
      }
    },
    clear() {
      try {
        storage?.removeItem(key);
      } catch {
        // Same as save.
      }
    },
  };
}
