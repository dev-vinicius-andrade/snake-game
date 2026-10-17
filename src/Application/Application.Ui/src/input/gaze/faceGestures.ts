import type { BlendshapeScore } from "./blendshapes";

/** Mouth and eyebrows, 0 (relaxed) to 1 (fully open or raised). */
export interface FaceGestures {
  jawOpen: number;
  brows: number;
}

const BROWS = ["browInnerUp", "browOuterUpLeft", "browOuterUpRight"] as const;
const REQUIRED = ["jawOpen", ...BROWS] as const;

const clamp01 = (value: number) => (Number.isFinite(value) ? Math.min(1, Math.max(0, value)) : 0);

/** Names of the gesture blendshapes the result does not contain, in the order they are used. */
export function missingFaceGestureBlendshapes(categories: readonly BlendshapeScore[]): string[] {
  const present = new Set(categories.map((category) => category.categoryName));
  return REQUIRED.filter((name) => !present.has(name));
}

/** Null when the face result does not carry every gesture blendshape. */
export function extractFaceGestures(categories: readonly BlendshapeScore[]): FaceGestures | null {
  const scores = new Map(categories.map((category) => [category.categoryName, clamp01(category.score)]));
  if (!REQUIRED.every((name) => scores.has(name))) return null;

  const brows = BROWS.reduce((sum, name) => sum + scores.get(name)!, 0) / BROWS.length;
  return { jawOpen: scores.get("jawOpen")!, brows };
}
