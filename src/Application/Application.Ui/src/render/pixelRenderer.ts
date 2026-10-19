import type { FloatingDamage, FoodSnapshot, GamePoint, GameSnapshot, RoomDimensions, ShotSnapshot, SnakeSnapshot } from "@/types/game";
import { GLYPH_HEIGHT, GLYPH_WIDTH, damageColor as damageNumberColor, damageLabel, damagePosition, glyphFor, isVisible } from "./damageNumbers";
import { DAMAGE_COLOR, contrastColor, headColor, lightenColor, segmentColor, shadeColor } from "./segmentColor";

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
  /** Clock in milliseconds, for things that glow. Without it they stay still. */
  timeMs?: number;
  /** Damage numbers on their way up the screen. Drawn only with `timeMs`. */
  damage?: FloatingDamage[];
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
  /** One pixel inside the block at `world`, `dx` and `dy` pixels from its top-left corner. */
  pixel(color: string, world: GamePoint, pixels: number, dx: number, dy: number): void;
}

/** Where blocks of a camera view go: which part of the wrapping world is on the canvas. */
interface Tiling {
  /** The canvas, in logical pixels. */
  view: { width: number; height: number };
  /** The world, in logical pixels. It repeats in every direction. */
  period: { width: number; height: number };
}

/**
 * Positions are rounded down to a pixel. The camera keeps the head exactly in the middle of the screen, so on a canvas with an
 * odd size the head and every body segment (a whole number of pixels apart) sit exactly on a pixel boundary, where a rounding
 * error of 1e-13 decides which side of it they fall on, and they jitter by a pixel from frame to frame. This bias, far larger than
 * any such error and far smaller than anything that can be seen, makes the decision the same every time.
 */
const PIXEL_BIAS = 1e-6;

const mod = (value: number, period: number) => ((value % period) + period) % period;

/** The places a block shows on one axis: where it is within one copy of the world, and one copy to the left of that. */
function copies(position: number, period: number, size: number, extent: number): number[] {
  const inside = mod(position, period);
  return [inside, inside - period].map(Math.floor).filter((at) => at + size > 0 && at < extent);
}

function createPainter(ctx: PixelContext, pixelSize: number, origin: GamePoint, tiling?: Tiling): Painter {
  /** Fills a `width` x `height` rectangle that starts `dx`, `dy` pixels inside the block of `pixels` at `world`. */
  function fill(color: string, world: GamePoint, pixels: number, dx: number, dy: number, width: number, height: number) {
    ctx.fillStyle = color;
    const left = (world.x - origin.x) / pixelSize + PIXEL_BIAS;
    const top = (world.y - origin.y) / pixelSize + PIXEL_BIAS;
    if (!tiling) {
      ctx.fillRect(Math.floor(left) + dx, Math.floor(top) + dy, width, height);
      return;
    }
    // The world wraps, so what leaves the canvas on one side of an edge is what enters on the other.
    for (const x of copies(left, tiling.period.width, pixels, tiling.view.width))
      for (const y of copies(top, tiling.period.height, pixels, tiling.view.height)) ctx.fillRect(x + dx, y + dy, width, height);
  }

  return {
    block: (color, world, pixels) => fill(color, world, pixels, 0, 0, pixels, pixels),
    pixel: (color, world, pixels, dx, dy) => fill(color, world, pixels, dx, dy, 1, 1),
  };
}

/** One dot per grid point that is on the canvas. The grid repeats with the world, so it needs no wrapping. */
function drawGrid(ctx: PixelContext, view: { width: number; height: number }, origin: GamePoint, pixelSize: number) {
  const first = { x: Math.ceil(origin.x / GRID_SPACING), y: Math.ceil(origin.y / GRID_SPACING) };
  const last = { x: Math.floor((origin.x + view.width * pixelSize) / GRID_SPACING), y: Math.floor((origin.y + view.height * pixelSize) / GRID_SPACING) };
  ctx.fillStyle = GRID_COLOR;
  for (let column = first.x; column <= last.x; column++)
    for (let row = first.y; row <= last.y; row++)
      ctx.fillRect(
        Math.floor((column * GRID_SPACING - origin.x) / pixelSize + PIXEL_BIAS),
        Math.floor((row * GRID_SPACING - origin.y) / pixelSize + PIXEL_BIAS),
        1,
        1
      );
}

const PRIZE_PIXELS = 5;
const PRIZE_RING_SHADE = 0.7;
const PRIZE_GLOW_LIGHTEN = 0.55;
const PRIZE_HEART = "#ffffff";
const PRIZE_GLOW_MS = 400;

/** A block with a darker ring and a white heart, in the colour of its kind, which flips between two shades. */
function drawPrize(painter: Painter, food: FoodSnapshot, timeMs: number) {
  const base = food.color.fillColor;
  const shades = [base, lightenColor(base, PRIZE_GLOW_LIGHTEN)];
  const gold = shades[Math.floor(timeMs / PRIZE_GLOW_MS) % shades.length];
  painter.block(shadeColor(base, PRIZE_RING_SHADE), food.position, PRIZE_PIXELS);
  for (let dx = 1; dx <= 3; dx++)
    for (let dy = 1; dy <= 3; dy++) painter.pixel(dx === 2 && dy === 2 ? PRIZE_HEART : gold, food.position, PRIZE_PIXELS, dx, dy);
}

function drawFood(painter: Painter, food: FoodSnapshot, timeMs: number) {
  if (food.kind === "prize") return drawPrize(painter, food, timeMs);
  painter.block(food.color.fillColor, food.position, food.value >= BIG_FOOD_VALUE ? BIG_FOOD_PIXELS : SMALL_FOOD_PIXELS);
}

const DAMAGE_SHADOW = "#000000";
/** Space between two characters of a damage number, in pixels. */
const GLYPH_GAP = 1;

/** One character of a damage number, with its top-left corner at `origin` (world units). */
function drawGlyph(painter: Painter, character: string, origin: GamePoint, pixelSize: number, color: string) {
  glyphFor(character).forEach((row, y) =>
    [...row].forEach((cell, x) => {
      if (cell === "#") painter.block(color, { x: origin.x + x * pixelSize, y: origin.y + y * pixelSize }, 1);
    })
  );
}

/** A damage number, centred over the point it floats at, with a dark shadow so it reads on any floor. */
function drawDamage(painter: Painter, damage: FloatingDamage, selfGuid: string | null | undefined, nowMs: number, pixelSize: number) {
  const label = damageLabel(damage);
  const at = damagePosition(damage, nowMs);
  const widthPixels = label.length * (GLYPH_WIDTH + GLYPH_GAP) - GLYPH_GAP;
  const left = at.x - (widthPixels * pixelSize) / 2;
  const top = at.y - (GLYPH_HEIGHT * pixelSize) / 2;
  const color = damageNumberColor(damage, selfGuid);
  for (const [shadow, tint] of [[1, DAMAGE_SHADOW], [0, color]] as const)
    [...label].forEach((character, index) =>
      drawGlyph(painter, character, { x: left + (index * (GLYPH_WIDTH + GLYPH_GAP) + shadow) * pixelSize, y: top + shadow * pixelSize }, pixelSize, tint)
    );
}

function drawShot(painter: Painter, shot: ShotSnapshot) {
  painter.block(SHOT_COLOR, shot.position, SHOT_PIXELS);
}

type Facing = "right" | "left" | "up" | "down";

/** Which way the head moves, from its last step. The short way round the world, so crossing an edge does not flip it. */
function facingOf(path: readonly GamePoint[], dimensions: RoomDimensions): Facing | null {
  if (path.length < 2) return null;
  const head = path[path.length - 1];
  const neck = path[path.length - 2];
  const shortest = (delta: number, size: number) => (delta > size / 2 ? delta - size : delta < -size / 2 ? delta + size : delta);
  const dx = shortest(head.x - neck.x, dimensions.width);
  const dy = shortest(head.y - neck.y, dimensions.height);
  if (dx === 0 && dy === 0) return null;
  if (Math.abs(dx) >= Math.abs(dy)) return dx > 0 ? "right" : "left";
  return dy > 0 ? "down" : "up";
}

/** The two eyes, as pixel offsets inside the head block, on the side it faces. */
function eyeOffsets(facing: Facing, pixels: number): Array<[number, number]> {
  const far = pixels - 1;
  switch (facing) {
    case "right":
      return [[far, 0], [far, far]];
    case "left":
      return [[0, 0], [0, far]];
    case "up":
      return [[0, 0], [far, 0]];
    default:
      return [[0, far], [far, far]];
  }
}

/** Every other segment, counting from the head, is a little darker: a straight snake shows its segments instead of looking like a bar. */
const ALTERNATE_SHADE = 0.82;

function drawSnake(painter: Painter, snake: SnakeSnapshot, segmentPixels: number, damageColor: string, dimensions: RoomDimensions) {
  const headIndex = snake.path.length - 1;
  snake.path.forEach((point, index) => {
    if (index === headIndex) return;
    const health = segmentColor(snake.color.fillColor, snake.segmentHealth[index] ?? 1, damageColor);
    painter.block((headIndex - index) % 2 === 1 ? shadeColor(health, ALTERNATE_SHADE) : health, point, segmentPixels);
  });
  if (headIndex < 0) return;

  const head = snake.path[headIndex];
  const headColorValue = headColor(snake.color.fillColor);
  painter.block(headColorValue, head, segmentPixels);
  const facing = facingOf(snake.path, dimensions);
  if (!facing) return;
  const eye = contrastColor(headColorValue);
  eyeOffsets(facing, segmentPixels).forEach(([dx, dy]) => painter.pixel(eye, head, segmentPixels, dx, dy));
}

/** Draws one frame: background, food, shots, other snakes, then the own snake on top. */
export function drawFrame(ctx: PixelContext, snapshot: GameSnapshot, options: RenderOptions = {}) {
  const pixelSize = options.pixelSize ?? DEFAULT_PIXEL_SIZE;
  const segmentPixels = Math.max(1, Math.round((options.headSize ?? DEFAULT_HEAD_SIZE) / pixelSize));
  const damageTint = options.damageColor ?? DAMAGE_COLOR;
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

  snapshot.foods.forEach((food) => drawFood(painter, food, options.timeMs ?? 0));
  snapshot.shots.forEach((shot) => drawShot(painter, shot));
  const living = snapshot.snakes.filter((snake) => snake.isAlive);
  const others = living.filter((snake) => snake.guid !== options.selfGuid);
  const own = living.filter((snake) => snake.guid === options.selfGuid);
  [...others, ...own].forEach((snake) => drawSnake(painter, snake, segmentPixels, damageTint, snapshot.room));
  const now = options.timeMs;
  if (now !== undefined)
    (options.damage ?? []).filter((entry) => isVisible(entry, now)).forEach((entry) => drawDamage(painter, entry, options.selfGuid, now, pixelSize));
}
