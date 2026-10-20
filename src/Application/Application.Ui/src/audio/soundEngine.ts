import { SOUNDS, type Note, type SoundId } from "./sounds";

/** What the game needs from a sound system. A spec swaps in a fake; the browser gets `WebAudioEngine`. */
export interface SoundEngine {
  /** Plays a sound now. Silent while muted, locked or unsupported; never throws. */
  play(id: SoundId): void;
  /** Allowed to start audio once the player has touched the page (browsers refuse before that). Call from a user gesture. */
  unlock(): void;
  setVolume(volume: number): void;
  setMuted(muted: boolean): void;
}

interface ParamLike {
  value: number;
  setValueAtTime(value: number, time: number): unknown;
  exponentialRampToValueAtTime(value: number, time: number): unknown;
}
interface NodeLike {
  connect(destination: unknown): unknown;
}
export interface GainNodeLike extends NodeLike {
  gain: ParamLike;
}
export interface OscillatorNodeLike extends NodeLike {
  type: string;
  frequency: ParamLike;
  start(time: number): void;
  stop(time: number): void;
}
export interface BufferSourceNodeLike extends NodeLike {
  buffer: unknown;
  start(time: number): void;
  stop(time: number): void;
}
interface BufferLike {
  getChannelData(channel: number): Float32Array;
}

/** The slice of the Web Audio API that is used, so a spec can stand in for the browser. */
export interface AudioContextLike {
  readonly currentTime: number;
  readonly sampleRate: number;
  readonly state: string;
  readonly destination: unknown;
  resume(): Promise<void> | void;
  createGain(): GainNodeLike;
  createOscillator(): OscillatorNodeLike;
  createBuffer(channels: number, length: number, sampleRate: number): BufferLike;
  createBufferSource(): BufferSourceNodeLike;
}

export interface WebAudioEngineOptions {
  /** Creates the audio context; null or a throw means this browser has none. Defaults to `AudioContext`. */
  createContext?: () => AudioContextLike | null;
  /** Clock in milliseconds, for the per-sound throttle. Defaults to `performance.now`. */
  now?: () => number;
  volume?: number;
  muted?: boolean;
}

/** The same sound is not started again inside this window: a burst of events (a shower of fruit) must not become a wall of noise. */
export const MIN_REPEAT_MS = 60;
/** Never louder than this, so full volume does not hurt. */
export const MASTER_CEILING = 0.6;
const NOISE_SECONDS = 1;

type ContextConstructor = new () => AudioContextLike;

const defaultContext = (): AudioContextLike | null => {
  const scope = globalThis as { AudioContext?: ContextConstructor; webkitAudioContext?: ContextConstructor };
  const Constructor = scope.AudioContext ?? scope.webkitAudioContext;
  return Constructor ? new Constructor() : null;
};

/** Chiptune sounds made on the fly with the Web Audio API. The context is only created by `unlock`, from a user gesture. */
export class WebAudioEngine implements SoundEngine {
  private context: AudioContextLike | null = null;
  private master: GainNodeLike | null = null;
  private noise: BufferLike | null = null;
  private unsupported = false;
  private volume: number;
  private muted: boolean;
  private lastPlayed = new Map<SoundId, number>();
  private readonly createContext: () => AudioContextLike | null;
  private readonly now: () => number;

  constructor(options: WebAudioEngineOptions = {}) {
    this.createContext = options.createContext ?? defaultContext;
    this.now = options.now ?? (() => performance.now());
    this.volume = options.volume ?? 0.5;
    this.muted = options.muted ?? false;
  }

  setVolume(volume: number) {
    this.volume = Math.min(1, Math.max(0, volume));
    this.applyVolume();
  }

  setMuted(muted: boolean) {
    this.muted = muted;
    this.applyVolume();
  }

  unlock() {
    if (this.unsupported) return;
    try {
      if (!this.context) {
        const context = this.createContext();
        if (!context) {
          this.unsupported = true;
          return;
        }
        this.context = context;
        this.master = context.createGain();
        this.master.connect(context.destination);
        this.applyVolume();
      }
      if (this.context.state === "suspended") void Promise.resolve(this.context.resume()).catch(() => undefined);
    } catch {
      this.unsupported = true;
      this.context = null;
      this.master = null;
    }
  }

  play(id: SoundId) {
    if (this.muted || this.volume <= 0 || !this.context || !this.master) return;
    const nowMs = this.now();
    const last = this.lastPlayed.get(id);
    if (last !== undefined && nowMs - last < MIN_REPEAT_MS) return;
    this.lastPlayed.set(id, nowMs);
    try {
      for (const note of SOUNDS[id]) this.schedule(this.context, this.master, note);
    } catch {
      // A browser that cannot make this sound just stays quiet.
    }
  }

  private applyVolume() {
    if (this.master) this.master.gain.value = this.muted ? 0 : this.volume * MASTER_CEILING;
  }

  private schedule(context: AudioContextLike, master: GainNodeLike, note: Note) {
    const begin = context.currentTime + note.start;
    const end = begin + note.duration;
    const envelope = context.createGain();
    envelope.gain.setValueAtTime(note.gain, begin);
    envelope.gain.exponentialRampToValueAtTime(0.001, end);
    envelope.connect(master);
    if (note.wave === "noise") {
      const source = context.createBufferSource();
      source.buffer = this.noiseBuffer(context);
      source.connect(envelope);
      source.start(begin);
      source.stop(end);
      return;
    }
    const oscillator = context.createOscillator();
    oscillator.type = note.wave;
    oscillator.frequency.setValueAtTime(note.from, begin);
    if (note.to !== undefined) oscillator.frequency.exponentialRampToValueAtTime(note.to, end);
    oscillator.connect(envelope);
    oscillator.start(begin);
    oscillator.stop(end);
  }

  /** White noise, made once and reused by every noise note. */
  private noiseBuffer(context: AudioContextLike): BufferLike {
    if (!this.noise) {
      const buffer = context.createBuffer(1, Math.floor(context.sampleRate * NOISE_SECONDS), context.sampleRate);
      const data = buffer.getChannelData(0);
      for (let i = 0; i < data.length; i++) data[i] = Math.random() * 2 - 1;
      this.noise = buffer;
    }
    return this.noise;
  }
}
