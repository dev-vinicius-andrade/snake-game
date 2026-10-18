export const DAMAGE_COLOR = "#ff2a2a";

type Rgb = readonly [number, number, number];

function parseHex(color: string): Rgb | null {
  const match = /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.exec(color.trim());
  if (!match) return null;
  const hex = match[1].length === 3 ? [...match[1]].map((digit) => digit + digit).join("") : match[1];
  return [0, 2, 4].map((offset) => parseInt(hex.slice(offset, offset + 2), 16)) as unknown as Rgb;
}

const toHex = (channel: number) => channel.toString(16).padStart(2, "0");

/**
 * Colour of a body segment: the snake colour at full health, fading to the damage colour as health drops.
 * Colours that are not `#rgb` / `#rrggbb` are returned unchanged.
 */
export function segmentColor(base: string, healthRatio: number, damageColor: string = DAMAGE_COLOR): string {
  const from = parseHex(base);
  const to = parseHex(damageColor);
  if (!from || !to) return base;

  const health = Number.isNaN(healthRatio) ? 1 : Math.min(1, Math.max(0, healthRatio));
  const damage = 1 - health;
  const mixed = from.map((channel, index) => Math.round(channel + (to[index] - channel) * damage));
  return `#${mixed.map(toHex).join("")}`;
}

const HEAD_SHIFT = 0.45;
/** Above this luminance a colour is too light to be made lighter, so the head goes darker instead. */
const LIGHT_LIMIT = 0.8;

const luminanceOf = ([r, g, b]: Rgb) => (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255;

/**
 * Colour of a snake's head: the body colour pushed toward white, or toward black when the body is already very light,
 * so the head can always be told from the body. The server sends the same fill and border colour, so this cannot come
 * from the border. Colours that are not `#rgb` / `#rrggbb` are returned unchanged.
 */
export function headColor(base: string): string {
  const from = parseHex(base);
  if (!from) return base;

  const target = luminanceOf(from) < LIGHT_LIMIT ? 255 : 0;
  const mixed = from.map((channel) => Math.round(channel + (target - channel) * HEAD_SHIFT));
  return `#${mixed.map(toHex).join("")}`;
}

/** The colour multiplied by `factor` (0..1) in every channel: darker. Colours that are not `#rgb` / `#rrggbb` are returned unchanged. */
export function shadeColor(base: string, factor: number): string {
  const from = parseHex(base);
  if (!from) return base;
  const clamped = Number.isNaN(factor) ? 1 : Math.min(1, Math.max(0, factor));
  return `#${from.map((channel) => toHex(Math.round(channel * clamped))).join("")}`;
}

const DARK = "#0b0d17";
const LIGHT = "#ffffff";

/** A colour that can be read on top of `base`: dark on light colours, white on dark ones. */
export function contrastColor(base: string): string {
  const from = parseHex(base);
  return from && luminanceOf(from) < 0.5 ? LIGHT : DARK;
}

