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
