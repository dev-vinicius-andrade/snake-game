import type { FoodSnapshot, GamePoint, GameSnapshot, RoomDimensions, ShotSnapshot, SnakeSnapshot } from "@/types/game";
import { DAMAGE_COLOR, segmentColor } from "./segmentColor";

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
}

const BACKGROUND = "#16161d";
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

function createPainter(ctx: PixelContext, pixelSize: number): Painter {
  return {
    block(color, world, pixels) {
      ctx.fillStyle = color;
      ctx.fillRect(Math.floor(world.x / pixelSize), Math.floor(world.y / pixelSize), pixels, pixels);
    },
  };
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
        ? snake.color.border
        : segmentColor(snake.color.fillColor, snake.segmentHealth[index] ?? 1, damageColor);
    painter.block(color, point, segmentPixels);
  });
}

/** Draws one frame: background, food, shots, other snakes, then the own snake on top. */
export function drawFrame(ctx: PixelContext, snapshot: GameSnapshot, options: RenderOptions = {}) {
  const pixelSize = options.pixelSize ?? DEFAULT_PIXEL_SIZE;
  const segmentPixels = Math.max(1, Math.round((options.headSize ?? DEFAULT_HEAD_SIZE) / pixelSize));
  const damageColor = options.damageColor ?? DAMAGE_COLOR;
  const painter = createPainter(ctx, pixelSize);

  const size = logicalSize(snapshot.room, pixelSize);
  ctx.fillStyle = options.backgroundColor ?? BACKGROUND;
  ctx.fillRect(0, 0, size.width, size.height);

  snapshot.foods.forEach((food) => drawFood(painter, food));
  snapshot.shots.forEach((shot) => drawShot(painter, shot));
  const living = snapshot.snakes.filter((snake) => snake.isAlive);
  const others = living.filter((snake) => snake.guid !== options.selfGuid);
  const own = living.filter((snake) => snake.guid === options.selfGuid);
  [...others, ...own].forEach((snake) => drawSnake(painter, snake, segmentPixels, damageColor));
}
