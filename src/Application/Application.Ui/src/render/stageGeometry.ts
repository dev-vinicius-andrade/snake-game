import type { GamePoint, RoomDimensions } from "@/types/game";
import { DEFAULT_HEAD_SIZE } from "./pixelRenderer";

/** Where the game canvas sits on the page, as `getBoundingClientRect` reports it. */
export interface StageRect {
  left: number;
  top: number;
  width: number;
  height: number;
}

/**
 * Page coordinates to stage coordinates: (0, 0) is the top-left of the canvas and (1, 1) the bottom-right.
 * Points outside the canvas are kept (they go below 0 or above 1) because aiming past the edge still has a direction.
 */
export function pointToStage(clientX: number, clientY: number, rect: StageRect): GamePoint | null {
  if (!(rect.width > 0) || !(rect.height > 0)) return null;
  return { x: (clientX - rect.left) / rect.width, y: (clientY - rect.top) / rect.height };
}

// Parameter is not called `room`: the auto-import plugin would mistake it for the default export of types/room.
export function stageToWorld(point: GamePoint, dimensions: RoomDimensions): GamePoint {
  return { x: point.x * dimensions.width, y: point.y * dimensions.height };
}

/** Snake positions are the top-left corner of the head box; aiming is measured from its middle. */
export function headCenter(head: GamePoint, headSize: number = DEFAULT_HEAD_SIZE): GamePoint {
  return { x: head.x + headSize / 2, y: head.y + headSize / 2 };
}
