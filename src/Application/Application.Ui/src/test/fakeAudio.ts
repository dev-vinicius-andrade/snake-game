import type { AudioContextLike, SoundEngine } from "@/audio/soundEngine";
import type { SoundId } from "@/audio/sounds";

/** A SoundEngine that only remembers what it was asked to play. */
export class FakeSoundEngine implements SoundEngine {
  played: SoundId[] = [];
  unlocked = 0;
  volume = 1;
  muted = false;

  play(id: SoundId) {
    this.played.push(id);
  }
  unlock() {
    this.unlocked++;
  }
  setVolume(volume: number) {
    this.volume = volume;
  }
  setMuted(muted: boolean) {
    this.muted = muted;
  }
  /** What was played since the last call. */
  take() {
    const taken = this.played;
    this.played = [];
    return taken;
  }
}

interface OscillatorRecord {
  type: string;
  from: number[];
  ramps: number[];
  started: number | null;
  stopped: number | null;
}

/** A stand-in for the browser's AudioContext that records the nodes it hands out. */
export class FakeAudioContext implements AudioContextLike {
  currentTime = 10;
  sampleRate = 100;
  state = "suspended";
  destination = { name: "destination" };
  resumed = 0;
  oscillators: OscillatorRecord[] = [];
  /** The master gain is the first one made. */
  gains: Array<{ value: number }> = [];
  noiseSources = 0;
  buffersMade = 0;

  resume() {
    this.resumed++;
    this.state = "running";
  }

  createGain() {
    const record = { value: 1 };
    this.gains.push(record);
    return {
      gain: {
        get value() {
          return record.value;
        },
        set value(next: number) {
          record.value = next;
        },
        setValueAtTime: () => undefined,
        exponentialRampToValueAtTime: () => undefined,
      },
      connect: () => undefined,
    };
  }

  createOscillator() {
    const record: OscillatorRecord = { type: "", from: [], ramps: [], started: null, stopped: null };
    this.oscillators.push(record);
    return {
      get type() {
        return record.type;
      },
      set type(next: string) {
        record.type = next;
      },
      frequency: {
        value: 0,
        setValueAtTime: (value: number) => void record.from.push(value),
        exponentialRampToValueAtTime: (value: number) => void record.ramps.push(value),
      },
      connect: () => undefined,
      start: (time: number) => void (record.started = time),
      stop: (time: number) => void (record.stopped = time),
    };
  }

  createBuffer(_channels: number, length: number) {
    this.buffersMade++;
    const data = new Float32Array(length);
    return { getChannelData: () => data };
  }

  createBufferSource() {
    this.noiseSources++;
    return { buffer: null as unknown, connect: () => undefined, start: () => undefined, stop: () => undefined };
  }
}
