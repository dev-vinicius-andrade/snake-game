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
/** The faint dots of the floor grid, fixed to the world so moving through it is visible. */
const GRID_COLOR = "#23263a";
/** World units between grid dots. 120 divides the default room (1920 x 1080), so the grid is seamless where the world wraps. */
const GRID_SPACING = 120;
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

/** Where blocks of a camera view go: which part of the wrapping world is on the canvas. */
interface Tiling {
  /** The canvas, in logical pixels. */
  view: { width: number; height: number };
  /** The world, in logical pixels. It repeats in every direction. */
  period: { width: number; height: number };
}

const mod = (value: number, period: number) => ((value % period) + period) % period;

/** The places a block shows on one axis: where it is within one copy of the world, and one copy to the left of that. */
function copies(position: number, period: number, size: number, extent: number): number[] {
  const inside = mod(position, period);
  return [inside, inside - period].map(Math.floor).filter((at) => at + size > 0 && at < extent);
}

function createPainter(ctx: PixelContext, pixelSize: number, origin: GamePoint, tiling?: Tiling): Painter {
  return {
    block(color, world, pixels) {
      ctx.fillStyle = color;
      const left = (world.x - origin.x) / pixelSize;
      const top = (world.y - origin.y) / pixelSize;
      if (!tiling) {
        ctx.fillRect(Math.floor(left), Math.floor(top), pixels, pixels);
        return;
      }
      // The world wraps, so what leaves the canvas on one side of an edge is what enters on the other.
      for (const x of copies(left, tiling.period.width, pixels, tiling.view.width))
        for (const y of copies(top, tiling.period.height, pixels, tiling.view.height)) ctx.fillRect(x, y, pixels, pixels);
    },
  };
}

/** One dot per grid point that is on the canvas. The grid repeats with the world, so it needs no wrapping. */
function drawGrid(ctx: PixelContext, view: { width: number; height: number }, origin: GamePoint, pixelSize: number) {
  const first = { x: Math.ceil(origin.x / GRID_SPACING), y: Math.ceil(origin.y / GRID_SPACING) };
  const last = { x: Math.floor((origin.x + view.width * pixelSize) / GRID_SPACING), y: Math.floor((origin.y + view.height * pixelSize) / GRID_SPACING) };
  ctx.fillStyle = GRID_COLOR;
  for (let column = first.x; column <= last.x; column++)
    for (let row = first.y; row <= last.y; row++)
      ctx.fillRect(Math.floor((column * GRID_SPACING - origin.x) / pixelSize), Math.floor((row * GRID_SPACING - origin.y) / pixelSize), 1, 1);
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
  const background = options.backgroundColor ?? BACKGROUND;
  const room = logicalSize(snapshot.room, pixelSize);
  const tiling = options.view
    ? { view: options.view, period: { width: snapshot.room.width / pixelSize, height: snapshot.room.height / pixelSize } }
    : undefined;
  const painter = createPainter(ctx, pixelSize, origin, tiling);

  ctx.fillStyle = background;
  ctx.fillRect(0, 0, options.view?.width ?? room.width, options.view?.height ?? room.height);
  if (options.view) drawGrid(ctx, options.view, origin, pixelSize);

  snapshot.foods.forEach((food) => drawFood(painter, food));
  snapshot.shots.forEach((shot) => drawShot(painter, shot));
  const living = snapshot.snakes.filter((snake) => snake.isAlive);
  const others = living.filter((snake) => snake.guid !== options.selfGuid);
  const own = living.filter((snake) => snake.guid === options.selfGuid);
  [...others, ...own].forEach((snake) => drawSnake(painter, snake, segmentPixels, damageColor));
}
