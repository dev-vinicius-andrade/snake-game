import type { BossSnapshot, MazeSnapshot, ZoneSnapshot, FloatingDamage, FoodSnapshot, GamePoint, GameSnapshot, RoomDimensions, ShotSnapshot, SnakeSnapshot } from "@/types/game";
import { GLYPH_HEIGHT, GLYPH_WIDTH, damageColor as damageNumberColor, damageLabel, damagePosition, glyphFor, isVisible } from "./damageNumbers";
import { armPixels, hpBarPixels } from "./boss";
import { wantedSnake } from "./bounty";
import { mazeLayoutOf, mazePrizeCenter, visibleBlocks, wallBlocks } from "./maze";
import { isMineNear, type MineBlast, type MineMark, type MineView } from "./mines";
import { discPoints, discRows, ringPoints, splashParticles, type StrikeRing, type StrikeFlash, type StrikeSplash, type StrikeView } from "./strikes";
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

import type { WorldClip } from "./maze";

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
  /** Lightning warnings and flashes. The warning is drawn under the snakes, the flash over everything. */
  strikes?: StrikeView;
  /** Mines and the blasts of those that went off. The mines are drawn under the snakes, the blasts over everything. */
  mines?: MineView;
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
  /** A run of `width` pixels in one row, starting at `world`. */
  row(color: string, world: GamePoint, width: number): void;
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
    row: (color, world, width) => fill(color, world, width, 0, 0, width, 1),
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

export const LIGHTNING_EDGE_COLOR = "#7a6a1f";
export const LIGHTNING_RING_COLOR = "#ffe14d";
const LIGHTNING_MARK_COLOR = "#b39b2a";
export const LIGHTNING_FLASH_COLOR = "#ffffff";
const LIGHTNING_AFTERGLOW_COLOR = "#9fc4ff";
/** Past this much of the warning the ring flickers, faster than it can be ignored. */
const LIGHTNING_URGENT_FROM = 0.75;
const LIGHTNING_FLICKER_MS = 90;
/** A flash is solid until this age, then every other pixel, then only the rim. */
const FLASH_SOLID_UNTIL = 0.4;
const FLASH_THIN_UNTIL = 0.75;

function drawLightningWarning(painter: Painter, ring: StrikeRing, pixelSize: number, urgent: boolean) {
  // Where it will strike: a dashed rim, and a mark in the middle.
  ringPoints(ring.center, ring.radius, pixelSize).forEach((point, index) => {
    if (index % 2 === 0) painter.block(LIGHTNING_EDGE_COLOR, point, 1);
  });
  painter.block(LIGHTNING_MARK_COLOR, ring.center, 1);
  if (ring.progress <= 0) return;
  const color = ring.progress >= LIGHTNING_URGENT_FROM && urgent ? LIGHTNING_FLASH_COLOR : LIGHTNING_RING_COLOR;
  ringPoints(ring.center, Math.max(ring.radius * ring.progress, pixelSize), pixelSize).forEach((point) => painter.block(color, point, 1));
}

export const METEOR_SHADOW_COLOR = "#0a0a12";
export const METEOR_RING_COLOR = "#ff7a2f";
const METEOR_EDGE_COLOR = "#7a2418";
export const METEOR_BODY_COLOR = "#ffd23f";
const METEOR_TRAIL_COLORS = ["#e8541f", "#c0392b", "#8f2d1c"];
export const METEOR_FLASH_COLOR = "#ffe9c4";
const METEOR_AFTERGLOW_COLOR = "#ff9a52";
/** Where a meteor comes from, relative to the spot it lands on: up and to the left, far enough to start off the screen. */
const METEOR_FALL = { x: -150, y: -420 };
const METEOR_BODY_PIXELS = 3;
const METEOR_TRAIL_PIXELS = 2;
/** World units between the blocks of a meteor's streak. */
const METEOR_TRAIL_STEP = 22;

/** A big shadow on the ground, striped so the floor shows through, its rim, a ring that grows to it, and the meteor falling in. */
function drawMeteorWarning(painter: Painter, ring: StrikeRing, pixelSize: number, urgent: boolean) {
  discRows(ring.center, ring.radius, pixelSize).forEach((row, index) => {
    if (index % 2 === 0) painter.row(METEOR_SHADOW_COLOR, { x: row.x, y: row.y }, row.width);
  });
  ringPoints(ring.center, ring.radius, pixelSize).forEach((point, index) => {
    if (index % 2 === 0) painter.block(METEOR_EDGE_COLOR, point, 1);
  });
  if (ring.progress <= 0) return;
  const color = ring.progress >= LIGHTNING_URGENT_FROM && urgent ? METEOR_FLASH_COLOR : METEOR_RING_COLOR;
  ringPoints(ring.center, Math.max(ring.radius * ring.progress, pixelSize), pixelSize).forEach((point) => painter.block(color, point, 1));
  if (ring.progress >= 1) return;

  const left = 1 - ring.progress;
  const at = { x: ring.center.x + METEOR_FALL.x * left, y: ring.center.y + METEOR_FALL.y * left };
  const length = Math.hypot(METEOR_FALL.x, METEOR_FALL.y);
  METEOR_TRAIL_COLORS.forEach((trail, index) =>
    painter.block(
      trail,
      { x: at.x + (METEOR_FALL.x / length) * METEOR_TRAIL_STEP * (index + 1), y: at.y + (METEOR_FALL.y / length) * METEOR_TRAIL_STEP * (index + 1) },
      METEOR_TRAIL_PIXELS
    )
  );
  painter.block(METEOR_BODY_COLOR, at, METEOR_BODY_PIXELS);
}

function drawStrikeWarning(painter: Painter, view: StrikeView, pixelSize: number, timeMs: number) {
  const urgent = Math.floor(timeMs / LIGHTNING_FLICKER_MS) % 2 === 1;
  for (const ring of view.rings)
    if (ring.kind === "meteor") drawMeteorWarning(painter, ring, pixelSize, urgent);
    else drawLightningWarning(painter, ring, pixelSize, urgent);
}

function drawLightningFlash(painter: Painter, flash: StrikeFlash, pixelSize: number) {
  if (flash.age < FLASH_SOLID_UNTIL) {
    discPoints(flash.center, flash.radius, pixelSize).forEach((point) => painter.block(LIGHTNING_FLASH_COLOR, point, 1));
  } else if (flash.age < FLASH_THIN_UNTIL) {
    discPoints(flash.center, flash.radius, pixelSize)
      .filter((point) => (Math.floor(point.x / pixelSize) + Math.floor(point.y / pixelSize)) % 2 === 0)
      .forEach((point) => painter.block(LIGHTNING_AFTERGLOW_COLOR, point, 1));
  } else {
    ringPoints(flash.center, flash.radius, pixelSize).forEach((point) => painter.block(LIGHTNING_AFTERGLOW_COLOR, point, 1));
  }
}

/** A meteor's impact is the same shape in fire colours, in rows of pixels: solid, then every other row, then only the rim. */
function drawMeteorFlash(painter: Painter, flash: StrikeFlash, pixelSize: number) {
  if (flash.age < FLASH_SOLID_UNTIL) {
    discRows(flash.center, flash.radius, pixelSize).forEach((row) => painter.row(METEOR_FLASH_COLOR, { x: row.x, y: row.y }, row.width));
  } else if (flash.age < FLASH_THIN_UNTIL) {
    discRows(flash.center, flash.radius, pixelSize).forEach((row, index) => {
      if (index % 2 === 0) painter.row(METEOR_AFTERGLOW_COLOR, { x: row.x, y: row.y }, row.width);
    });
  } else {
    ringPoints(flash.center, flash.radius, pixelSize).forEach((point) => painter.block(METEOR_AFTERGLOW_COLOR, point, 1));
  }
}

export const SPLASH_LIGHTNING_COLORS: readonly string[] = ["#ffffff", "#ffe14d", "#9fc4ff"];
export const SPLASH_METEOR_COLORS: readonly string[] = ["#ffe9c4", "#ff7a2f", "#c0392b"];
/** A splash keeps a bright blob where the body was struck for this much of its life. */
const SPLASH_BLOB_UNTIL = 0.25;

/** The bits that fly off a body that was struck: a blob where it was, and bits flung outwards that dim as they go. */
function drawSplash(painter: Painter, splash: StrikeSplash) {
  const colors = splash.kind === "meteor" ? SPLASH_METEOR_COLORS : SPLASH_LIGHTNING_COLORS;
  if (splash.age < SPLASH_BLOB_UNTIL) painter.block(colors[0], splash.center, 2);
  splashParticles(splash).forEach((particle) => painter.block(colors[particle.shade], particle, 1));
}

function drawStrikeFlash(painter: Painter, view: StrikeView, pixelSize: number) {
  for (const flash of view.flashes)
    if (flash.kind === "meteor") drawMeteorFlash(painter, flash, pixelSize);
    else drawLightningFlash(painter, flash, pixelSize);
  view.splashes.forEach((splash) => drawSplash(painter, splash));
}

export const ZONE_RING_COLOR = "#5fd0ff";
export const ZONE_FINAL_COLOR = "#2a8fb5";
export const ZONE_DANGER_COLOR = "#6b1d2a";
/** How many rows of pixels the danger tint reaches outside the edge, and how far from the edge it starts. */
const ZONE_DANGER_FROM = 2;
const ZONE_DANGER_TO = 5;

/**
 * The safe zone. While it warns only the safe spot is marked, a dashed circle: the zone itself covers the whole room. Once it
 * closes its edge is a ring, the ground just outside it is tinted in dots, and the spot where it will end up stays marked.
 */
function drawZone(painter: Painter, zone: ZoneSnapshot, pixelSize: number) {
  ringPoints(zone.position, zone.finalRadius, pixelSize).forEach((point, index) => {
    if (index % 2 === 0) painter.block(ZONE_FINAL_COLOR, point, 1);
  });
  if (zone.phase === "warning") return;
  for (let row = ZONE_DANGER_FROM; row <= ZONE_DANGER_TO; row++)
    ringPoints(zone.position, zone.radius + row * pixelSize, pixelSize).forEach((point, index) => {
      if ((index + row) % 2 === 0) painter.block(ZONE_DANGER_COLOR, point, 1);
    });
  ringPoints(zone.position, zone.radius, pixelSize).forEach((point) => painter.block(ZONE_RING_COLOR, point, 1));
}

export const BOSS_BODY_COLOR = "#3b2f4a";
const BOSS_CORE_COLOR = "#ff9ab0";
export const BOSS_BARREL_COLOR = "#c9b6e8";
export const BOSS_SHOT_COLOR = "#ff4d6d";
const BOSS_BAR_BACK_COLOR = "#14101c";
export const BOSS_BAR_GOOD_COLOR = "#4cd964";
export const BOSS_BAR_HURT_COLOR = "#ffcc33";
export const BOSS_BAR_LOW_COLOR = "#ff4d4d";
const BOSS_BAR_ROWS = 2;

/** A square with a glowing core, barrels sticking out of it, and a hit point bar above that goes from green to red. */
function drawBoss(painter: Painter, boss: BossSnapshot, pixelSize: number) {
  const pixels = Math.max(1, Math.round(boss.size / pixelSize));
  const corner = { x: boss.position.x - boss.size / 2, y: boss.position.y - boss.size / 2 };
  painter.block(BOSS_BODY_COLOR, corner, pixels);
  painter.block(BOSS_CORE_COLOR, { x: boss.position.x - pixelSize, y: boss.position.y - pixelSize }, 2);
  armPixels(boss, pixelSize).forEach((point) => painter.block(BOSS_BARREL_COLOR, point, 1));

  const bar = hpBarPixels(boss, pixelSize);
  const fill = boss.hpRatio > 0.5 ? BOSS_BAR_GOOD_COLOR : boss.hpRatio > 0.25 ? BOSS_BAR_HURT_COLOR : BOSS_BAR_LOW_COLOR;
  for (let row = 0; row < BOSS_BAR_ROWS; row++) {
    const at = { x: bar.origin.x, y: bar.origin.y + row * pixelSize };
    painter.row(BOSS_BAR_BACK_COLOR, at, bar.width);
    painter.row(fill, at, bar.filled);
  }
}

export const MINE_BODY_COLOR = "#2e3140";
export const MINE_SPIKE_COLOR = "#8a8f9e";
export const MINE_ARMED_LIGHT_COLOR = "#ff3b3b";
export const MINE_ARMED_LIGHT_OFF_COLOR = "#5a1010";
export const MINE_DIM_BODY_COLOR = "#24262f";
const MINE_DIM_SPIKE_COLOR = "#4a4e5c";
export const MINE_ARMING_LIGHT_COLOR = "#b0b4c0";
export const MINE_ARMING_LIGHT_OFF_COLOR = "#3a3d48";
export const MINE_RING_COLOR = "#4a2630";
export const MINE_RING_NEAR_COLOR = "#ff4d4d";
export const MINE_BLAST_COLOR = "#fff1c1";
const MINE_AFTERGLOW_COLOR = "#ff6a2f";
/** An armed mine's light is on for this long, then off as long; one that is arming blinks faster. */
const MINE_ARMED_BLINK_MS = 400;
const MINE_ARMING_BLINK_MS = 150;
/** A naval mine, 5 by 5 pixels: `s` a spike, `b` the body, `l` the light in the middle. */
const MINE_SPRITE = ["s.s.s", ".bbb.", "sblbs", ".bbb.", "s.s.s"];
const MINE_SPRITE_PIXELS = 5;

/** A mine and the reach of its blast: a faint dashed ring, red and denser when a snake's head is near an armed one. */
function drawMine(painter: Painter, mark: MineMark, snapshot: GameSnapshot, pixelSize: number, timeMs: number) {
  const danger = mark.armed && isMineNear(mark, snapshot);
  ringPoints(mark.center, mark.radius, pixelSize).forEach((point, index) => {
    if (index % (danger ? 2 : 3) === 0) painter.block(danger ? MINE_RING_NEAR_COLOR : MINE_RING_COLOR, point, 1);
  });

  const lit = mark.armed ? Math.floor(timeMs / MINE_ARMED_BLINK_MS) % 2 === 0 : Math.floor(timeMs / MINE_ARMING_BLINK_MS) % 2 === 0;
  const colors: Record<string, string> = mark.armed
    ? { s: MINE_SPIKE_COLOR, b: MINE_BODY_COLOR, l: lit ? MINE_ARMED_LIGHT_COLOR : MINE_ARMED_LIGHT_OFF_COLOR }
    : { s: MINE_DIM_SPIKE_COLOR, b: MINE_DIM_BODY_COLOR, l: lit ? MINE_ARMING_LIGHT_COLOR : MINE_ARMING_LIGHT_OFF_COLOR };
  const corner = { x: mark.center.x - 2 * pixelSize, y: mark.center.y - 2 * pixelSize };
  MINE_SPRITE.forEach((row, dy) =>
    [...row].forEach((cell, dx) => {
      if (cell !== ".") painter.pixel(colors[cell], corner, MINE_SPRITE_PIXELS, dx, dy);
    })
  );
}

/** A mine going off is the shape of its blast in fire colours, in rows of pixels: solid, then every other row, then only the rim. */
function drawMineBlast(painter: Painter, blast: MineBlast, pixelSize: number) {
  if (blast.age < FLASH_SOLID_UNTIL) {
    discRows(blast.center, blast.radius, pixelSize).forEach((row) => painter.row(MINE_BLAST_COLOR, { x: row.x, y: row.y }, row.width));
  } else if (blast.age < FLASH_THIN_UNTIL) {
    discRows(blast.center, blast.radius, pixelSize).forEach((row, index) => {
      if (index % 2 === 0) painter.row(MINE_AFTERGLOW_COLOR, { x: row.x, y: row.y }, row.width);
    });
  } else {
    ringPoints(blast.center, blast.radius, pixelSize).forEach((point) => painter.block(MINE_AFTERGLOW_COLOR, point, 1));
  }
}

/** Two stones of a wall, laid like a checkerboard, the light along the top of a wall that has floor above and the shade along the bottom. */
export const MAZE_WALL_COLORS = ["#4c5070", "#454968"];
export const MAZE_WALL_LIGHT_COLOR = "#8e93b8";
export const MAZE_WALL_SHADE_COLOR = "#2a2c42";
export const MAZE_PRIZE_GLOW_COLOR = "#ffe27a";
const MAZE_GLOW_BLINK_MS = 300;

/**
 * The walls of a maze, as blocks of stone of one head size, in the world units of the server. The walls are not in the snapshot: they
 * are built from its seed, and only the blocks the camera shows are drawn. The prize at the far end blinks a ring of light.
 */
function drawMaze(painter: Painter, maze: MazeSnapshot, clip: WorldClip, world: RoomDimensions, pixelSize: number, timeMs: number) {
  const layout = mazeLayoutOf(maze);
  const blockPixels = Math.max(1, Math.round(maze.unit / pixelSize));
  for (const block of wallBlocks(maze, layout, visibleBlocks(maze, layout, clip, world))) {
    painter.block(MAZE_WALL_COLORS[(block.column + block.row) % 2], block.position, blockPixels);
    if (block.lit) painter.row(MAZE_WALL_LIGHT_COLOR, block.position, blockPixels);
    if (block.shaded) painter.row(MAZE_WALL_SHADE_COLOR, { x: block.position.x, y: block.position.y + (blockPixels - 1) * pixelSize }, blockPixels);
  }
  if (Math.floor(timeMs / MAZE_GLOW_BLINK_MS) % 2 === 0) {
    const radius = (maze.corridor * maze.unit) / 2;
    ringPoints(mazePrizeCenter(maze), radius, pixelSize).forEach((point, index) => {
      if (index % 2 === 0) painter.block(MAZE_PRIZE_GLOW_COLOR, point, 1);
    });
  }
}

export const BOUNTY_CROWN_COLOR = "#ffd23f";
export const BOUNTY_GEM_COLOR = "#ff3b3b";
const BOUNTY_BAND_COLOR = "#b8860b";
const BOUNTY_SHADOW_COLOR = "#000000";
/** A crown, 7 by 5 pixels: `g` gold, `r` a red gem, `d` the darker band at the bottom. */
const BOUNTY_CROWN = ["g..g..g", "gg.g.gg", "ggggggg", "grgrgrg", "ddddddd"];
const BOUNTY_CROWN_WIDTH = 7;
/** Pixels between the crown and the head. */
const BOUNTY_GAP = 2;
/** The crown is up one pixel for this long, then down as long. */
const BOUNTY_BOB_MS = 400;

/** A crown over the head of the snake with a price on it, that every player sees, with a dark shadow so it reads on any floor. */
function drawBountyMark(painter: Painter, head: GamePoint, segmentPixels: number, pixelSize: number, timeMs: number) {
  const colors: Record<string, string> = { g: BOUNTY_CROWN_COLOR, r: BOUNTY_GEM_COLOR, d: BOUNTY_BAND_COLOR };
  const bob = Math.floor(timeMs / BOUNTY_BOB_MS) % 2;
  const corner = {
    x: head.x + ((segmentPixels - BOUNTY_CROWN_WIDTH) / 2) * pixelSize,
    y: head.y - (BOUNTY_CROWN.length + BOUNTY_GAP + bob) * pixelSize,
  };
  for (const shadow of [1, 0])
    BOUNTY_CROWN.forEach((row, dy) =>
      [...row].forEach((cell, dx) => {
        if (cell !== ".") painter.pixel(shadow ? BOUNTY_SHADOW_COLOR : colors[cell], corner, BOUNTY_CROWN_WIDTH, dx + shadow, dy + shadow);
      })
    );
}

function drawShot(painter: Painter, shot: ShotSnapshot, bossGuid?: string) {
  painter.block(bossGuid !== undefined && shot.ownerGuid === bossGuid ? BOSS_SHOT_COLOR : SHOT_COLOR, shot.position, SHOT_PIXELS);
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

const SHIELD_COLOR = "#7fe9ff";
const SHIELD_BLINK_MS = 160;

/** A ring of pixels one pixel out from the head, blinking, for a snake nothing can hurt for the moment. */
export function drawShield(painter: Painter, head: GamePoint, segmentPixels: number, timeMs: number) {
  if (Math.floor(timeMs / SHIELD_BLINK_MS) % 2 === 1) return;
  const last = segmentPixels;
  for (let offset = -1; offset <= last; offset++) {
    painter.pixel(SHIELD_COLOR, head, segmentPixels, offset, -1);
    painter.pixel(SHIELD_COLOR, head, segmentPixels, offset, last);
    if (offset >= 0 && offset < last) {
      painter.pixel(SHIELD_COLOR, head, segmentPixels, -1, offset);
      painter.pixel(SHIELD_COLOR, head, segmentPixels, last, offset);
    }
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

  if (snapshot.maze) {
    const clip = options.view
      ? { left: origin.x, top: origin.y, right: origin.x + options.view.width * pixelSize, bottom: origin.y + options.view.height * pixelSize }
      : { left: 0, top: 0, right: snapshot.room.width, bottom: snapshot.room.height };
    drawMaze(painter, snapshot.maze, clip, snapshot.room, pixelSize, options.timeMs ?? 0);
  }
  if (options.strikes) drawStrikeWarning(painter, options.strikes, pixelSize, options.timeMs ?? 0);
  snapshot.foods.forEach((food) => drawFood(painter, food, options.timeMs ?? 0));
  if (snapshot.zone) drawZone(painter, snapshot.zone, pixelSize);
  options.mines?.mines.forEach((mark) => drawMine(painter, mark, snapshot, pixelSize, options.timeMs ?? 0));
  if (snapshot.boss) drawBoss(painter, snapshot.boss, pixelSize);
  snapshot.shots.forEach((shot) => drawShot(painter, shot, snapshot.boss?.guid));
  const living = snapshot.snakes.filter((snake) => snake.isAlive);
  const others = living.filter((snake) => snake.guid !== options.selfGuid);
  const own = living.filter((snake) => snake.guid === options.selfGuid);
  [...others, ...own].forEach((snake) => drawSnake(painter, snake, segmentPixels, damageTint, snapshot.room));
  living.filter((snake) => (snake.protectedMs ?? 0) > 0 && snake.path.length > 0).forEach((snake) =>
    drawShield(painter, snake.path[snake.path.length - 1], segmentPixels, options.timeMs ?? 0)
  );
  const wanted = wantedSnake(snapshot);
  if (wanted) drawBountyMark(painter, wanted.head, segmentPixels, pixelSize, options.timeMs ?? 0);
  if (options.strikes) drawStrikeFlash(painter, options.strikes, pixelSize);
  options.mines?.blasts.forEach((blast) => drawMineBlast(painter, blast, pixelSize));
  const now = options.timeMs;
  if (now !== undefined)
    (options.damage ?? []).filter((entry) => isVisible(entry, now)).forEach((entry) => drawDamage(painter, entry, options.selfGuid, now, pixelSize));
}
