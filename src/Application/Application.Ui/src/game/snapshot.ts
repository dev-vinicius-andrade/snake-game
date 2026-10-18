import type {
  FoodSnapshot,
  GameColor,
  GamePoint,
  GameSnapshot,
  LeaderboardEntry,
  RoomDimensions,
  ShotSnapshot,
  SnakeDirection,
  SnakeSnapshot,
} from "@/types/game";

type Dictionary = Record<string, unknown>;

const DEFAULT_COLOR: GameColor = { fillColor: "#ffffff", border: "#000000" };
const DIRECTIONS: readonly SnakeDirection[] = ["left", "up", "right", "down", "angular"];

const isDictionary = (value: unknown): value is Dictionary =>
  typeof value === "object" && value !== null && !Array.isArray(value);
const isFinite = (value: unknown): value is number => typeof value === "number" && Number.isFinite(value);
const asString = (value: unknown): string | undefined => (typeof value === "string" ? value : undefined);

function parsePoint(raw: unknown): GamePoint | null {
  return isDictionary(raw) && isFinite(raw.x) && isFinite(raw.y) ? { x: raw.x, y: raw.y } : null;
}

function parseColor(raw: unknown): GameColor {
  if (!isDictionary(raw)) return { ...DEFAULT_COLOR };
  return {
    fillColor: asString(raw.fillColor) ?? DEFAULT_COLOR.fillColor,
    border: asString(raw.border) ?? DEFAULT_COLOR.border,
  };
}

function parseDirection(raw: unknown): SnakeDirection {
  return DIRECTIONS.find((direction) => direction === raw) ?? "right";
}

/** Server dictionaries are keyed by guid; keep key and value together, skip values that are not objects. */
function dictionaryEntries(raw: unknown): Array<[string, Dictionary]> {
  if (!isDictionary(raw)) return [];
  return Object.entries(raw).filter((entry): entry is [string, Dictionary] => isDictionary(entry[1]));
}

const clamp01 = (value: number) => Math.min(1, Math.max(0, value));

/** Health is aligned with the head: a short list is padded at the tail side, a long one trimmed there. */
function alignHealth(raw: unknown, length: number): number[] {
  const values = Array.isArray(raw) ? raw.map((value) => (isFinite(value) ? clamp01(value) : 1)) : [];
  const padded = [...Array(Math.max(0, length - values.length)).fill(1), ...values];
  return padded.slice(padded.length - length);
}

function parsePath(player: Dictionary): GamePoint[] {
  const path = Array.isArray(player.path) ? player.path.map(parsePoint).filter((p): p is GamePoint => p !== null) : [];
  if (path.length > 0) return path;
  const position = parsePoint(player.currentPosition);
  return position ? [position] : [];
}

function parseSnake([key, player]: [string, Dictionary]): SnakeSnapshot | null {
  const path = parsePath(player);
  if (path.length === 0) return null;
  return {
    guid: asString(player.guid) ?? key,
    connectionId: asString(player.id) ?? "",
    name: asString(player.name) ?? "",
    head: path[path.length - 1],
    path,
    segmentHealth: alignHealth(player.segmentHealth, path.length),
    isAlive: typeof player.isAlive === "boolean" ? player.isAlive : true,
    isBoosting: player.isBoosting === true,
    perks: Array.isArray(player.perks) ? player.perks.filter((perk): perk is string => typeof perk === "string") : [],
    autoFire: player.autoFire === true,
    foodsEaten: isFinite(player.foodsEaten) ? player.foodsEaten : 0,
    score: isFinite(player.score) ? player.score : 0,
    color: parseColor(player.backgroundColor),
    direction: parseDirection(player.currentDirection),
  };
}

function parseFood([key, food]: [string, Dictionary]): FoodSnapshot | null {
  const position = parsePoint(food.position);
  if (!position) return null;
  return {
    guid: asString(food.guid) ?? key,
    kind: food.kind === "prize" ? "prize" : "normal",
    position,
    color: parseColor(food.color),
    value: isFinite(food.value) && food.value > 0 ? food.value : 1,
  };
}

function parseShot([key, shot]: [string, Dictionary]): ShotSnapshot | null {
  const position = parsePoint(shot.currentPosition);
  if (!position) return null;
  const owner = isDictionary(shot.createdyBy) ? asString(shot.createdyBy.guid) : undefined;
  return {
    guid: asString(shot.guid) ?? key,
    position,
    size: isFinite(shot.size) ? shot.size : 0,
    ownerGuid: owner,
  };
}

export function parseRoomSize(raw: unknown): RoomDimensions | null {
  if (!isDictionary(raw) || !isFinite(raw.width) || !isFinite(raw.height)) return null;
  return raw.width > 0 && raw.height > 0 ? { width: raw.width, height: raw.height } : null;
}

/** Turns the `GameStateUpdated` payload into the client model. Returns null when it is not a usable snapshot. */
export function parseSnapshot(raw: unknown): GameSnapshot | null {
  if (!isDictionary(raw)) return null;
  const size = parseRoomSize(raw.room);
  if (!size) return null;

  const room = raw.room as Dictionary;
  return {
    room: { guid: asString(room.guid) ?? null, ...size },
    snakes: dictionaryEntries(raw.players).map(parseSnake).filter((s): s is SnakeSnapshot => s !== null),
    foods: dictionaryEntries(raw.foods).map(parseFood).filter((f): f is FoodSnapshot => f !== null),
    shots: dictionaryEntries(raw.obstacles).map(parseShot).filter((s): s is ShotSnapshot => s !== null),
  };
}

/** Turns the `ScoreUpdated` payload into leaderboard rows, keeping the server order. */
export function parseLeaderboard(raw: unknown): LeaderboardEntry[] {
  if (!Array.isArray(raw)) return [];
  return raw
    .filter((entry): entry is Dictionary => isDictionary(entry))
    .filter((entry) => typeof entry.nickname === "string" && isFinite(entry.currentScore))
    .map((entry) => ({ nickname: entry.nickname as string, currentScore: entry.currentScore as number }));
}
