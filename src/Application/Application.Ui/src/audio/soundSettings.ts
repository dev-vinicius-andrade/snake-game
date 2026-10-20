export interface SoundSettings {
  muted: boolean;
  /** 0 (silent) to 1 (full). */
  volume: number;
}

export const SOUND_SETTINGS_KEY = "snake.sound";
export const DEFAULT_SOUND_SETTINGS: SoundSettings = { muted: false, volume: 0.5 };

export const clampVolume = (volume: number) => Math.min(1, Math.max(0, volume));

/** The browser storage, or null where it is blocked (private window, site data blocked). */
export function browserStorage(): Storage | null {
  try {
    return window.localStorage;
  } catch {
    return null;
  }
}

/** What was saved, or the defaults when nothing usable is there. Never throws. */
export function loadSoundSettings(storage: Storage | null = browserStorage()): SoundSettings {
  try {
    const raw = storage?.getItem(SOUND_SETTINGS_KEY);
    if (!raw) return { ...DEFAULT_SOUND_SETTINGS };
    const parsed = JSON.parse(raw) as Partial<SoundSettings> | null;
    return {
      muted: typeof parsed?.muted === "boolean" ? parsed.muted : DEFAULT_SOUND_SETTINGS.muted,
      volume: typeof parsed?.volume === "number" && Number.isFinite(parsed.volume) ? clampVolume(parsed.volume) : DEFAULT_SOUND_SETTINGS.volume,
    };
  } catch {
    return { ...DEFAULT_SOUND_SETTINGS };
  }
}

/** Saves the settings; silently does nothing where storage is blocked. */
export function saveSoundSettings(settings: SoundSettings, storage: Storage | null = browserStorage()): void {
  try {
    storage?.setItem(SOUND_SETTINGS_KEY, JSON.stringify(settings));
  } catch {
    // Blocked or full: the settings just last for this visit.
  }
}
