/** Every sound the game makes. */
export const SOUND_IDS = [
  "eat",
  "prize",
  "shoot",
  "hit",
  "hurt",
  "broke",
  "die",
  "kill",
  "boost",
  "lightning",
  "meteor",
  "mine",
  "turretHit",
  "bountyStart",
  "bountyClaimed",
  "shower",
  "rush",
  "mazeOpen",
  "mazeEnter",
  "mazeWall",
  "mazePrize",
  "saved",
  "ability",
] as const;

export type SoundId = (typeof SOUND_IDS)[number];

export type Wave = "square" | "triangle" | "sawtooth" | "sine" | "noise";

/** One blip of a chiptune sound: a tone (or noise) that may slide from one pitch to another. */
export interface Note {
  /** Seconds after the sound starts. */
  start: number;
  duration: number;
  wave: Wave;
  /** Hz at the start; ignored for noise. */
  from: number;
  /** Hz at the end, when the tone slides. */
  to?: number;
  /** 0..1, before the master volume. */
  gain: number;
}

const tone = (start: number, duration: number, wave: Wave, from: number, to: number | undefined, gain: number): Note => ({ start, duration, wave, from, to, gain });
/** A run of equal short notes, one per pitch. */
const arpeggio = (pitches: number[], step: number, wave: Wave, gain: number): Note[] => pitches.map((pitch, index) => tone(index * step, step * 1.1, wave, pitch, undefined, gain));

/** Pixel-art recipes: square and triangle waves, quick slides, a little noise. Nothing is downloaded; the browser synthesises them. */
export const SOUNDS: Record<SoundId, Note[]> = {
  eat: [tone(0, 0.05, "square", 520, 880, 0.22)],
  prize: arpeggio([523, 659, 784, 1047, 1319], 0.07, "square", 0.25),
  shoot: [tone(0, 0.12, "square", 900, 180, 0.2), tone(0, 0.06, "noise", 0, undefined, 0.08)],
  hit: [tone(0, 0.06, "triangle", 320, 220, 0.25)],
  hurt: [tone(0, 0.18, "sawtooth", 220, 90, 0.3), tone(0, 0.1, "noise", 0, undefined, 0.15)],
  broke: [tone(0, 0.22, "square", 180, 60, 0.3), tone(0, 0.2, "noise", 0, undefined, 0.22)],
  die: [tone(0, 0.2, "square", 440, 330, 0.3), tone(0.2, 0.2, "square", 330, 220, 0.3), tone(0.4, 0.5, "sawtooth", 220, 55, 0.3)],
  kill: [tone(0, 0.08, "square", 392, undefined, 0.25), tone(0.08, 0.08, "square", 523, undefined, 0.25), tone(0.16, 0.2, "square", 784, undefined, 0.28)],
  boost: [tone(0, 0.18, "sawtooth", 200, 700, 0.15)],
  lightning: [tone(0, 0.35, "noise", 0, undefined, 0.35), tone(0, 0.3, "sawtooth", 1400, 70, 0.25)],
  meteor: [tone(0, 0.5, "noise", 0, undefined, 0.4), tone(0, 0.5, "triangle", 160, 40, 0.4)],
  mine: [tone(0, 0.45, "noise", 0, undefined, 0.45), tone(0, 0.3, "square", 140, 35, 0.35)],
  turretHit: [tone(0, 0.05, "square", 700, undefined, 0.2), tone(0.05, 0.08, "triangle", 260, 180, 0.2)],
  bountyStart: [tone(0, 0.15, "square", 330, undefined, 0.25), tone(0.18, 0.15, "square", 330, undefined, 0.25), tone(0.36, 0.3, "square", 494, undefined, 0.28)],
  bountyClaimed: [...arpeggio([392, 494, 587, 784], 0.08, "square", 0.25), tone(0.32, 0.3, "triangle", 988, undefined, 0.2)],
  shower: arpeggio([1319, 1175, 1047, 880, 784, 659], 0.06, "triangle", 0.2),
  rush: arpeggio([330, 392, 494, 587, 659, 784, 988], 0.045, "square", 0.18),
  mazeOpen: [tone(0, 0.3, "triangle", 110, 220, 0.3), tone(0.3, 0.4, "triangle", 220, 330, 0.3)],
  mazeEnter: arpeggio([262, 330, 392], 0.09, "triangle", 0.25),
  mazeWall: [tone(0, 0.07, "square", 150, 100, 0.25), tone(0, 0.05, "noise", 0, undefined, 0.12)],
  saved: [...arpeggio([392, 523, 659, 784, 1047], 0.06, "triangle", 0.3), tone(0.3, 0.35, "square", 1047, 1568, 0.22)],
  ability: [tone(0, 0.1, "square", 330, 660, 0.2), tone(0.08, 0.16, "triangle", 660, 990, 0.22)],
  mazePrize: [...arpeggio([523, 659, 784, 1047, 784, 1047, 1319], 0.08, "square", 0.25), tone(0.56, 0.5, "triangle", 1319, undefined, 0.2)],
};

/** Seconds from the start of a sound to the end of its last note. */
export function soundDuration(id: SoundId): number {
  return Math.max(...SOUNDS[id].map((note) => note.start + note.duration));
}
