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

/**
 * Top-left of the view in world coordinates when it follows `target`: the target is always in the middle. The world wraps
 * around, so there are no edges to stop at: next to a wall the view simply shows the far side of the world, and the
 * renderer draws the world tiled to fill it.
 */
export function cameraOrigin(target: GamePoint, view: { width: number; height: number }): GamePoint {
  return { x: target.x - view.width / 2, y: target.y - view.height / 2 };
}

/** A world point measured from the top-left of the view. */
export function toViewPoint(point: GamePoint, origin: GamePoint): GamePoint {
  return { x: point.x - origin.x, y: point.y - origin.y };
}

/** Everything the stage needs to draw one frame of the camera and to turn pointer positions into world positions. */
export interface Camera {
  /** What the camera follows, in world coordinates. */
  target: GamePoint;
  /** How far the target is from the middle of the view, in world units. Zero when the camera follows exactly. */
  offset: GamePoint;
  /** World position of the top-left corner of the view. */
  origin: GamePoint;
  /** The part of the world in view, in world units. Aim sources use it as the size of their "room". */
  size: { width: number; height: number };
  /** The canvas size in logical pixels. */
  view: { width: number; height: number };
}

const shortestDelta = (delta: number, size: number) => (delta > size / 2 ? delta - size : delta < -size / 2 ? delta + size : delta);

/**
 * The camera for a screen, centred on `center` (the target itself unless the camera is made to follow a little behind it).
 * The world wraps, so the offset between the two is measured the short way round.
 */
export function followCamera(layout: ViewLayout, world: RoomDimensions, target: GamePoint, center: GamePoint = target): Camera {
  const size = { width: layout.worldWidth, height: layout.worldHeight };
  return {
    target,
    offset: { x: shortestDelta(target.x - center.x, world.width), y: shortestDelta(target.y - center.y, world.height) },
    origin: cameraOrigin(center, size),
    size,
    view: { width: layout.logicalWidth, height: layout.logicalHeight },
  };
}

/** Where the target is on the view, in world units from its top-left corner. */
export function targetOnScreen(camera: Camera): GamePoint {
  return { x: camera.size.width / 2 + camera.offset.x, y: camera.size.height / 2 + camera.offset.y };
}
