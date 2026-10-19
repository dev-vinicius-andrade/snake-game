import pack from "./gameIcons.json";

/** Names of the icons of game-icons.net (CC BY 3.0) are written `game-icons:name`; the ones that ship are in gameIconNames.json. */
export const GAME_ICON_PREFIX = "game-icons:";

const bodies = pack.icons as Record<string, string>;

/** The size, in its own units, the bodies are drawn in. */
export const GAME_ICON_SIZE = pack.width;

/** `arrow-cluster` for `game-icons:arrow-cluster`; null for anything else. */
export function gameIconName(icon: string | null | undefined): string | null {
  return icon && icon.startsWith(GAME_ICON_PREFIX) && icon.length > GAME_ICON_PREFIX.length ? icon.slice(GAME_ICON_PREFIX.length) : null;
}

/** The drawing of a game icon that ships, as the inside of an svg. Null when it is not one of ours. */
export function gameIconBody(icon: string | null | undefined): string | null {
  const name = gameIconName(icon);
  return name !== null && Object.prototype.hasOwnProperty.call(bodies, name) ? bodies[name] : null;
}

export type IconSource = { kind: "game"; body: string } | { kind: "mdi"; name: string };

const FALLBACK = "mdi-star-four-points";

/** How to draw an icon name: a game icon from its body, a Material Design icon by its name, a star when there is nothing to draw. */
export function iconSource(icon: string | null | undefined): IconSource {
  const body = gameIconBody(icon);
  if (body) return { kind: "game", body };
  return icon && icon.startsWith("mdi-") ? { kind: "mdi", name: icon } : { kind: "mdi", name: FALLBACK };
}
