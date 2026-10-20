import type { ControlDirection } from "@/types/game";

/** What the player wants, independent of the device that expressed it (keyboard, pointer, eyes). */
export type ControlIntent =
  | { type: "direction"; direction: ControlDirection }
  | { type: "steer"; angle: number }
  | { type: "shoot" }
  /** The shoot control that sent `shoot` was let go. With auto fire, shooting goes on until then. */
  | { type: "shootRelease" }
  | { type: "boost"; active: boolean }
  /** Use the cooldown perks that are ready (shield burst, dash, heal pulse, vacuum burst). */
  | { type: "ability" };
