import { describe, expect, it } from "vitest";
import { DEFAULT_SOUND_SETTINGS, SOUND_SETTINGS_KEY, loadSoundSettings, saveSoundSettings } from "./soundSettings";

function memoryStorage(initial: Record<string, string> = {}): Storage {
  const data = new Map(Object.entries(initial));
  return {
    get length() {
      return data.size;
    },
    clear: () => data.clear(),
    getItem: (key) => data.get(key) ?? null,
    key: (index) => [...data.keys()][index] ?? null,
    removeItem: (key) => void data.delete(key),
    setItem: (key, value) => void data.set(key, value),
  };
}

const throwing: Storage = new Proxy({} as Storage, {
  get() {
    throw new Error("blocked");
  },
});

describe("sound settings", () => {
  it("starts with sound on at a moderate volume", () => {
    expect(loadSoundSettings(memoryStorage())).toEqual(DEFAULT_SOUND_SETTINGS);
    expect(DEFAULT_SOUND_SETTINGS.muted).toBe(false);
  });
  it("reads back what was saved", () => {
    const storage = memoryStorage();
    saveSoundSettings({ muted: true, volume: 0.25 }, storage);
    expect(loadSoundSettings(storage)).toEqual({ muted: true, volume: 0.25 });
  });
  it("falls back to the defaults on garbage", () => {
    expect(loadSoundSettings(memoryStorage({ [SOUND_SETTINGS_KEY]: "{nope" }))).toEqual(DEFAULT_SOUND_SETTINGS);
    expect(loadSoundSettings(memoryStorage({ [SOUND_SETTINGS_KEY]: '{"muted":"yes","volume":"loud"}' }))).toEqual(DEFAULT_SOUND_SETTINGS);
  });
  it("clamps the volume to 0..1", () => {
    expect(loadSoundSettings(memoryStorage({ [SOUND_SETTINGS_KEY]: '{"muted":false,"volume":7}' })).volume).toBe(1);
    expect(loadSoundSettings(memoryStorage({ [SOUND_SETTINGS_KEY]: '{"muted":false,"volume":-1}' })).volume).toBe(0);
  });
  it("survives storage that throws", () => {
    expect(loadSoundSettings(throwing)).toEqual(DEFAULT_SOUND_SETTINGS);
    expect(() => saveSoundSettings({ muted: true, volume: 0.5 }, throwing)).not.toThrow();
  });
  it("survives no storage at all", () => {
    expect(loadSoundSettings(null)).toEqual(DEFAULT_SOUND_SETTINGS);
    expect(() => saveSoundSettings(DEFAULT_SOUND_SETTINGS, null)).not.toThrow();
  });
});
