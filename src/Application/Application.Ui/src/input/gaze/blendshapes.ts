/** One MediaPipe blendshape: `categoryName` such as `eyeLookOutLeft`, `score` from 0 to 1. */
export interface BlendshapeScore {
  categoryName: string;
  score: number;
}

/**
 * What the eyes are doing, from the face blendshapes.
 * `x` is negative looking to the subject's left and positive to the right, `y` is negative looking up and
 * positive looking down (screen direction), both in -1..1. `blink` is 0 (open) to 1 (shut).
 */
export interface EyeSignals {
  x: number;
  y: number;
  blink: number;
}

/**
 * MediaPipe names blendshapes from the subject's point of view, like ARKit: `eyeLookOutLeft` is the subject's left
 * eye turning toward the subject's left. A mirrored camera image would invert them, so feed it the raw video.
 */
const REQUIRED = [
  "eyeLookInLeft",
  "eyeLookOutLeft",
  "eyeLookUpLeft",
  "eyeLookDownLeft",
  "eyeLookInRight",
  "eyeLookOutRight",
  "eyeLookUpRight",
  "eyeLookDownRight",
  "eyeBlinkLeft",
  "eyeBlinkRight",
] as const;

/** Names of the eye blendshapes the result does not contain, in the order they are used. Empty when it is complete. */
export function missingEyeBlendshapes(categories: readonly BlendshapeScore[]): string[] {
  const present = new Set(categories.map((category) => category.categoryName));
  return REQUIRED.filter((name) => !present.has(name));
}

const clamp01 = (value: number) => (Number.isFinite(value) ? Math.min(1, Math.max(0, value)) : 0);

/** Null when the face result does not carry every eye blendshape. */
export function extractEyeSignals(categories: readonly BlendshapeScore[]): EyeSignals | null {
  const scores = new Map(categories.map((category) => [category.categoryName, clamp01(category.score)]));
  if (!REQUIRED.every((name) => scores.has(name))) return null;
  const score = (name: (typeof REQUIRED)[number]) => scores.get(name)!;

  const lookRight = (score("eyeLookOutRight") + score("eyeLookInLeft")) / 2;
  const lookLeft = (score("eyeLookOutLeft") + score("eyeLookInRight")) / 2;
  const lookDown = (score("eyeLookDownLeft") + score("eyeLookDownRight")) / 2;
  const lookUp = (score("eyeLookUpLeft") + score("eyeLookUpRight")) / 2;
  return {
    x: lookRight - lookLeft,
    y: lookDown - lookUp,
    blink: (score("eyeBlinkLeft") + score("eyeBlinkRight")) / 2,
  };
}
