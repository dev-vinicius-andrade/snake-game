import type { GamePoint, RoomDimensions } from "@/types/game";
import { DEFAULT_PIXEL_SIZE } from "./pixelRenderer";

/** About how many logical pixels the short side of the screen shows: it decides how much of the arena is in view. */
export const TARGET_SHORT_SIDE = 120;
/** Even the smallest window keeps chunky pixels. */
export const MIN_SCALE = 2;

/** How the screen maps onto the game: the pixel art grid, and how much of the world it shows. */
export interface ViewLayout {
  /** Screen pixels per logical pixel, a whole number so the pixel art stays sharp. */
  scale: number;
  logicalWidth: number;
  logicalHeight: number;
  /** The part of the world the screen shows, in world units. */
  worldWidth: number;
  worldHeight: number;
}

export interface ViewLayoutOptions {
  pixelSize?: number;
  targetShortSide?: number;
  /** When given, the view is zoomed in as far as needed to never show more than this world on either axis. */
  world?: RoomDimensions;
}

/** The smallest scale at which the screen shows no more than the whole world on either axis. */
function fitScale(cssWidth: number, cssHeight: number, world: RoomDimensions, pixelSize: number): number {
  return Math.max(Math.ceil(cssWidth / (world.width / pixelSize)), Math.ceil(cssHeight / (world.height / pixelSize)));
}

/**
 * Sizes the view for a screen of `cssWidth` x `cssHeight`: a whole-number scale that shows about
 * {@link TARGET_SHORT_SIDE} logical pixels on the short side, rounded up so the canvas covers the screen.
 * Null when the screen has no usable size.
 */
export function viewLayout(cssWidth: number, cssHeight: number, options: ViewLayoutOptions = {}): ViewLayout | null {
  if (!(cssWidth > 0) || !(cssHeight > 0)) return null;
  const pixelSize = options.pixelSize ?? DEFAULT_PIXEL_SIZE;
  const target = options.targetShortSide ?? TARGET_SHORT_SIDE;

  const wanted = Math.max(MIN_SCALE, Math.round(Math.min(cssWidth, cssHeight) / target));
  const scale = options.world ? Math.max(wanted, fitScale(cssWidth, cssHeight, options.world, pixelSize)) : wanted;
  const logicalWidth = Math.ceil(cssWidth / scale);
  const logicalHeight = Math.ceil(cssHeight / scale);
  return { scale, logicalWidth, logicalHeight, worldWidth: logicalWidth * pixelSize, worldHeight: logicalHeight * pixelSize };
}

function axisOrigin(target: number, viewSize: number, worldSize: number): number {
  if (viewSize >= worldSize) return (worldSize - viewSize) / 2;
  return Math.min(worldSize - viewSize, Math.max(0, target - viewSize / 2));
}

/**
 * Top-left of the view in world coordinates when it follows `target`: the target is in the middle, except near the
 * edges of the world where the view stops, and on an axis where the view is larger than the world, which is centred.
 */
export function cameraOrigin(target: GamePoint, view: { width: number; height: number }, world: RoomDimensions): GamePoint {
  return { x: axisOrigin(target.x, view.width, world.width), y: axisOrigin(target.y, view.height, world.height) };
}

/** A world point measured from the top-left of the view. */
export function toViewPoint(point: GamePoint, origin: GamePoint): GamePoint {
  return { x: point.x - origin.x, y: point.y - origin.y };
}

/** Everything the stage needs to draw one frame of the camera and to turn pointer positions into world positions. */
export interface Camera {
  /** World position of the top-left corner of the view. */
  origin: GamePoint;
  /** The part of the world in view, in world units. Aim sources use it as the size of their "room". */
  size: { width: number; height: number };
  /** The canvas size in logical pixels. */
  view: { width: number; height: number };
}

/** The camera for a screen showing `world`, following `target`. */
export function followCamera(layout: ViewLayout, world: RoomDimensions, target: GamePoint): Camera {
  const size = { width: layout.worldWidth, height: layout.worldHeight };
  return {
    origin: cameraOrigin(target, size, world),
    size,
    view: { width: layout.logicalWidth, height: layout.logicalHeight },
  };
}
