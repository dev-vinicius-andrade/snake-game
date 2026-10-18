import type { FoodSnapshot, GamePoint, GameSnapshot, RoomDimensions, ShotSnapshot, SnakeSnapshot } from "@/types/game";
import { DAMAGE_COLOR, headColor, segmentColor } from "./segmentColor";

/** The 2D canvas methods the renderer needs, so it can be tested without a browser. */
export interface PixelContext {
  fillStyle: string | CanvasGradient | CanvasPattern;
  fillRect(x: number, y: number, width: number, height: number): void;
}

/**
 * World units per logical pixel. The canvas is drawn at logical size and scaled up by CSS, so every
 * sprite is made of chunky pixels.
 */
export const DEFAULT_PIXEL_SIZE = 5;
/** Side of a snake segment in world units. Mirrors the server `SnakeConfiguration.HeadSize`. */
export const DEFAULT_HEAD_SIZE = 15;

export interface RenderOptions {
  pixelSize?: number;
  headSize?: number;
  backgroundColor?: string;
  damageColor?: string;
  /** The player's own snake is drawn last so it is never hidden. */
  selfGuid?: string | null;
  /**
   * Camera: the size of the canvas in logical pixels, and the world position of its top-left corner. Without a view the
   * whole room is drawn.
   */
  view?: { width: number; height: number };
  origin?: GamePoint;
}

const BACKGROUND = "#16161d";
/** Outside the arena, and the line along its edge. */
const VOID = "#07080f";
const EDGE = "#3a4670";
const SHOT_COLOR = "#ffffff";
const SMALL_FOOD_PIXELS = 2;
const BIG_FOOD_PIXELS = 3;
const BIG_FOOD_VALUE = 5;
const SHOT_PIXELS = 2;

// Parameter is not called `room`: the auto-import plugin would mistake it for the default export of types/room.
export function logicalSize(dimensions: RoomDimensions, pixelSize: number = DEFAULT_PIXEL_SIZE) {
  return { width: Math.ceil(dimensions.width / pixelSize), height: Math.ceil(dimensions.height / pixelSize) };
}

interface Painter {
  block(color: string, world: GamePoint, pixels: number): void;
}

function createPainter(ctx: PixelContext, pixelSize: number, origin: GamePoint): Painter {
  return {
    block(color, world, pixels) {
      ctx.fillStyle = color;
      ctx.fillRect(Math.floor((world.x - origin.x) / pixelSize), Math.floor((world.y - origin.y) / pixelSize), pixels, pixels);
    },
  };
}

/** The arena on the screen: the void everywhere, the arena where it is, and a thin line along its edge. */
function drawArena(ctx: PixelContext, snapshot: GameSnapshot, view: { width: number; height: number }, origin: GamePoint, pixelSize: number, background: string) {
  const arena = logicalSize(snapshot.room, pixelSize);
  const left = Math.floor(-origin.x / pixelSize);
  const top = Math.floor(-origin.y / pixelSize);

  ctx.fillStyle = VOID;
  ctx.fillRect(0, 0, view.width, view.height);
  ctx.fillStyle = background;
  ctx.fillRect(left, top, arena.width, arena.height);
  ctx.fillStyle = EDGE;
  ctx.fillRect(left - 1, top - 1, arena.width + 2, 1);
  ctx.fillRect(left - 1, top + arena.height, arena.width + 2, 1);
  ctx.fillRect(left - 1, top, 1, arena.height);
  ctx.fillRect(left + arena.width, top, 1, arena.height);
}

function drawFood(painter: Painter, food: FoodSnapshot) {
  painter.block(food.color.fillColor, food.position, food.value >= BIG_FOOD_VALUE ? BIG_FOOD_PIXELS : SMALL_FOOD_PIXELS);
}

function drawShot(painter: Painter, shot: ShotSnapshot) {
  painter.block(SHOT_COLOR, shot.position, SHOT_PIXELS);
}

function drawSnake(painter: Painter, snake: SnakeSnapshot, segmentPixels: number, damageColor: string) {
  const headIndex = snake.path.length - 1;
  snake.path.forEach((point, index) => {
    const color =
      index === headIndex
        ? headColor(snake.color.fillColor)
        : segmentColor(snake.color.fillColor, snake.segmentHealth[index] ?? 1, damageColor);
    painter.block(color, point, segmentPixels);
  });
}

/** Draws one frame: background, food, shots, other snakes, then the own snake on top. */
export function drawFrame(ctx: PixelContext, snapshot: GameSnapshot, options: RenderOptions = {}) {
  const pixelSize = options.pixelSize ?? DEFAULT_PIXEL_SIZE;
  const segmentPixels = Math.max(1, Math.round((options.headSize ?? DEFAULT_HEAD_SIZE) / pixelSize));
  const damageColor = options.damageColor ?? DAMAGE_COLOR;
  const origin = options.origin ?? { x: 0, y: 0 };
  const painter = createPainter(ctx, pixelSize, origin);
  const background = options.backgroundColor ?? BACKGROUND;

  if (options.view) {
    drawArena(ctx, snapshot, options.view, origin, pixelSize, background);
  } else {
    const size = logicalSize(snapshot.room, pixelSize);
    ctx.fillStyle = background;
    ctx.fillRect(0, 0, size.width, size.height);
  }

  snapshot.foods.forEach((food) => drawFood(painter, food));
  snapshot.shots.forEach((shot) => drawShot(painter, shot));
  const living = snapshot.snakes.filter((snake) => snake.isAlive);
  const others = living.filter((snake) => snake.guid !== options.selfGuid);
  const own = living.filter((snake) => snake.guid === options.selfGuid);
  [...others, ...own].forEach((snake) => drawSnake(painter, snake, segmentPixels, damageColor));
}
