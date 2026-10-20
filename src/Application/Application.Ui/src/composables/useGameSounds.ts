import { getCurrentScope, onScopeDispose, ref, watch, type Ref } from "vue";
import { SoundDirector } from "@/audio/soundDirector";
import { WebAudioEngine, type SoundEngine } from "@/audio/soundEngine";
import { browserStorage, clampVolume, loadSoundSettings, saveSoundSettings } from "@/audio/soundSettings";
import type { FloatingDamage, GameSnapshot } from "@/types/game";

export interface UseGameSoundsOptions {
  snapshot: Readonly<Ref<GameSnapshot | null>>;
  damage: Readonly<Ref<FloatingDamage[]>>;
  /** Becomes non-null when the own snake dies. */
  death: Readonly<Ref<unknown>>;
  selfGuid: () => string | null | undefined;
  engine?: SoundEngine;
  storage?: Storage | null;
  /** Where the first user gesture is heard. Defaults to `window`. */
  unlockTarget?: EventTarget | null;
}

/** Events that count as the player touching the page, which is what lets a browser start audio. */
const GESTURES = ["pointerdown", "keydown", "touchstart"];

/** Sound for a game session: plays what the snapshots and notices say, and holds the mute and volume settings (saved). */
export function useGameSounds(options: UseGameSoundsOptions) {
  const storage = options.storage === undefined ? browserStorage() : options.storage;
  const saved = loadSoundSettings(storage);
  const muted = ref(saved.muted);
  const volume = ref(saved.volume);
  const engine = options.engine ?? new WebAudioEngine({ muted: saved.muted, volume: saved.volume });
  engine.setMuted(muted.value);
  engine.setVolume(volume.value);
  const director = new SoundDirector(engine);

  watch(
    options.snapshot,
    (snapshot) => (snapshot ? director.observe(snapshot, options.selfGuid()) : director.reset()),
    { flush: "sync" }
  );
  watch(options.damage, (events) => director.damage(events, options.selfGuid()), { flush: "sync" });
  watch(options.death, (death) => death && director.died(), { flush: "sync" });

  const target = options.unlockTarget === undefined ? (typeof window === "undefined" ? null : window) : options.unlockTarget;
  const onGesture = () => {
    engine.unlock();
    stopListening();
  };
  function stopListening() {
    GESTURES.forEach((name) => target?.removeEventListener(name, onGesture));
  }
  GESTURES.forEach((name) => target?.addEventListener(name, onGesture, { passive: true }));
  if (getCurrentScope()) onScopeDispose(stopListening);

  const persist = () => saveSoundSettings({ muted: muted.value, volume: volume.value }, storage);

  function toggleMute() {
    muted.value = !muted.value;
    engine.setMuted(muted.value);
    engine.unlock();
    persist();
  }

  /** Changes the volume and plays a short sound so the player hears the new level. */
  function setVolume(next: number) {
    volume.value = clampVolume(Number.isFinite(next) ? next : volume.value);
    engine.setVolume(volume.value);
    engine.unlock();
    persist();
    if (!muted.value) engine.play("eat");
  }

  return { muted, volume, toggleMute, setVolume, unlock: () => engine.unlock() };
}
