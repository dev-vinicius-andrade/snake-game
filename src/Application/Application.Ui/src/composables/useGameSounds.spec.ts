import { effectScope, nextTick, ref, shallowRef } from "vue";
import { describe, expect, it } from "vitest";
import { SOUND_SETTINGS_KEY, loadSoundSettings } from "@/audio/soundSettings";
import { FakeSoundEngine } from "@/test/fakeAudio";
import type { FloatingDamage, GameSnapshot, SnakeSnapshot } from "@/types/game";
import { useGameSounds } from "./useGameSounds";

const room = { guid: null, width: 4000, height: 4000 };
const color = { fillColor: "#00ff00", border: "#00ff00" };
const own = (foodsEaten: number): SnakeSnapshot => ({
  guid: "me",
  connectionId: "c",
  name: "me",
  head: { x: 10, y: 10 },
  path: [{ x: 10, y: 10 }],
  segmentHealth: [1],
  isAlive: true,
  isBoosting: false,
  perks: [],
  perkTiers: [],
  autoFire: false,
  foodsEaten,
  score: 0,
  color,
  direction: "right",
});
const frame = (foodsEaten = 0): GameSnapshot => ({ room, snakes: [own(foodsEaten)], foods: [], shots: [], strikes: [], boss: null, zone: null });

function memoryStorage(): Storage {
  const data = new Map<string, string>();
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

function setup(storage = memoryStorage()) {
  const engine = new FakeSoundEngine();
  const snapshot = shallowRef<GameSnapshot | null>(null);
  const damage = shallowRef<FloatingDamage[]>([]);
  const death = shallowRef<{ score: number } | null>(null);
  const selfGuid = ref<string | undefined>("me");
  const gesture = new EventTarget();
  const scope = effectScope();
  const sounds = scope.run(() => useGameSounds({ engine, storage, unlockTarget: gesture, snapshot, damage, death, selfGuid: () => selfGuid.value }))!;
  return { engine, snapshot, damage, death, gesture, sounds, scope, storage };
}

describe("useGameSounds", () => {
  it("plays what the snapshots say", async () => {
    const { engine, snapshot } = setup();
    snapshot.value = frame(0);
    snapshot.value = frame(1);
    await nextTick();
    expect(engine.take()).toEqual(["eat"]);
  });

  it("plays damage notices and death once", async () => {
    const { engine, snapshot, damage, death } = setup();
    snapshot.value = frame();
    damage.value = [{ shooterGuid: "x", targetGuid: "me", x: 0, y: 0, damage: 1, kind: "hit", bornMs: 0 }];
    death.value = { score: 3 };
    await nextTick();
    expect(engine.take()).toEqual(["hurt", "die"]);
  });

  it("forgets the last snapshot when the game ends, so the next room starts quiet", async () => {
    const { engine, snapshot } = setup();
    snapshot.value = frame(0);
    snapshot.value = null;
    snapshot.value = frame(5);
    await nextTick();
    expect(engine.take()).toEqual([]);
  });

  it("unlocks the engine on the first user gesture only", () => {
    const { engine, gesture } = setup();
    expect(engine.unlocked).toBe(0);
    gesture.dispatchEvent(new Event("pointerdown"));
    gesture.dispatchEvent(new Event("keydown"));
    expect(engine.unlocked).toBe(1);
  });

  it("stops listening for gestures when the scope ends", () => {
    const { engine, gesture, scope } = setup();
    scope.stop();
    gesture.dispatchEvent(new Event("pointerdown"));
    expect(engine.unlocked).toBe(0);
  });

  it("applies the saved settings to the engine at once", () => {
    const storage = memoryStorage();
    storage.setItem(SOUND_SETTINGS_KEY, JSON.stringify({ muted: true, volume: 0.2 }));
    const { engine, sounds } = setup(storage);
    expect(engine.muted).toBe(true);
    expect(engine.volume).toBeCloseTo(0.2);
    expect(sounds.muted.value).toBe(true);
    expect(sounds.volume.value).toBeCloseTo(0.2);
  });

  it("toggling mute reaches the engine, unlocks it, and is saved", () => {
    const { engine, sounds, storage } = setup();
    sounds.toggleMute();
    expect(engine.muted).toBe(true);
    expect(engine.unlocked).toBe(1);
    expect(loadSoundSettings(storage).muted).toBe(true);
    sounds.toggleMute();
    expect(engine.muted).toBe(false);
    expect(loadSoundSettings(storage).muted).toBe(false);
  });

  it("changing the volume clamps it, saves it, and gives a sample to hear", () => {
    const { engine, sounds, storage } = setup();
    sounds.setVolume(2);
    expect(sounds.volume.value).toBe(1);
    expect(engine.volume).toBe(1);
    expect(loadSoundSettings(storage).volume).toBe(1);
    expect(engine.take()).toEqual(["eat"]);
  });

  it("gives no sample while muted", () => {
    const { engine, sounds } = setup();
    sounds.toggleMute();
    engine.take();
    sounds.setVolume(0.3);
    expect(engine.take()).toEqual([]);
  });
});
