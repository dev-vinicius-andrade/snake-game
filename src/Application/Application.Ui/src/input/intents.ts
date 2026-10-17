import type { ControlDirection } from "@/types/game";

/** What the player wants, independent of the device that expressed it (keyboard, pointer, eyes). */
export type ControlIntent =
  | { type: "direction"; direction: ControlDirection }
  | { type: "steer"; angle: number }
  | { type: "shoot" }
  | { type: "boost"; active: boolean };
