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
  StrikeSnapshot,
} from "@/types/game";

type Dictionary = Record<string, unknown>;

const DEFAULT_COLOR: GameColor = { fillColor: "#ffffff", border: "#000000" };
const DIRECTIONS: readonly SnakeDirection[] = ["left", "up", "right", "down", "angular"];

const isDictionary = (value: unknown): value is Dictionary =>
  typeof value === "object" && value !== null && !Array.isArray(value);
const isFinite = (value: unknown): value is number => typeof value === "number" && Number.isFinite(value);
const asString = (value: unknown): string | undefined => (typeof value === "string" ? value : undefined);

function parseDirection(raw: unknown): SnakeDirection {
  return DIRECTIONS.find((direction) => direction === raw) ?? "right";
}

const clamp01 = (value: number) => Math.min(1, Math.max(0, value));
const asList = (value: unknown): unknown[] => (Array.isArray(value) ? value : []);

/** Health arrives in whole percents, aligned with the head: a short list is padded at the tail side, a long one trimmed there. */
function alignHealth(raw: unknown, length: number): number[] {
  const values = asList(raw).map((value) => (isFinite(value) ? clamp01(value / 100) : 1));
  const padded = [...Array(Math.max(0, length - values.length)).fill(1), ...values];
  return padded.slice(padded.length - length);
}

/** The path comes as one flat list, x0, y0, x1, y1, ...; a pair with something that is not a number is dropped. */
function parsePath(raw: unknown): GamePoint[] {
  const flat = asList(raw);
  const points: GamePoint[] = [];
  for (let index = 0; index + 1 < flat.length; index += 2) {
    const x = flat[index];
    const y = flat[index + 1];
    if (isFinite(x) && isFinite(y)) points.push({ x, y });
  }
  return points;
}

/** The server sends one colour; the client keeps the fill and border pair it draws with. */
function parseSingleColor(raw: unknown): GameColor {
  const color = asString(raw);
  return color ? { fillColor: color, border: color } : { ...DEFAULT_COLOR };
}

function parseSnake(player: unknown): SnakeSnapshot | null {
  if (!isDictionary(player)) return null;
  const path = parsePath(player.path);
  if (path.length === 0) return null;
  return {
    guid: asString(player.guid) ?? "",
    connectionId: asString(player.id) ?? "",
    name: asString(player.name) ?? "",
    head: path[path.length - 1],
    path,
    segmentHealth: alignHealth(player.health, path.length),
    isAlive: player.dead !== true,
    isBoosting: player.boost === true,
    perks: asList(player.perks).filter((perk): perk is string => typeof perk === "string"),
    autoFire: player.autoFire === true,
    foodsEaten: isFinite(player.foodsEaten) ? player.foodsEaten : 0,
    score: isFinite(player.score) ? player.score : 0,
    color: parseSingleColor(player.color),
    direction: parseDirection(player.direction),
  };
}

/** Food has no identity on the wire; the place it lies at is what tells two apart. */
function parseFood(food: unknown): FoodSnapshot | null {
  if (!isDictionary(food) || !isFinite(food.x) || !isFinite(food.y)) return null;
  return {
    guid: `${food.x},${food.y}`,
    kind: food.k === 1 ? "prize" : "normal",
    position: { x: food.x, y: food.y },
    color: parseSingleColor(food.c),
    value: isFinite(food.v) && food.v > 0 ? food.v : 1,
  };
}

function parseShot(shot: unknown): ShotSnapshot | null {
  if (!isDictionary(shot) || !isFinite(shot.x) || !isFinite(shot.y)) return null;
  return {
    guid: asString(shot.guid) ?? `${shot.x},${shot.y}`,
    position: { x: shot.x, y: shot.y },
    size: isFinite(shot.size) ? shot.size : 0,
    ownerGuid: asString(shot.owner),
  };
}

/** A lightning warning: where, how far it reaches (`r`) and the milliseconds left (`t`). Like food, the place is its identity. */
function parseStrike(strike: unknown): StrikeSnapshot | null {
  if (!isDictionary(strike) || !isFinite(strike.x) || !isFinite(strike.y) || !isFinite(strike.r) || strike.r <= 0) return null;
  return {
    guid: `${strike.x},${strike.y}`,
    position: { x: strike.x, y: strike.y },
    radius: strike.r,
    msLeft: isFinite(strike.t) ? Math.max(0, strike.t) : 0,
  };
}

export function parseRoomSize(raw: unknown): RoomDimensions | null {
  if (!isDictionary(raw) || !isFinite(raw.width) || !isFinite(raw.height)) return null;
  return raw.width > 0 && raw.height > 0 ? { width: raw.width, height: raw.height } : null;
}

/** Turns the `Snapshot` payload into the client model. Returns null when it is not a usable snapshot. */
export function parseSnapshot(raw: unknown): GameSnapshot | null {
  if (!isDictionary(raw)) return null;
  const size = parseRoomSize(raw.room);
  if (!size) return null;

  const room = raw.room as Dictionary;
  return {
    room: { guid: asString(room.guid) ?? null, ...size },
    snakes: asList(raw.snakes).map(parseSnake).filter((s): s is SnakeSnapshot => s !== null),
    foods: asList(raw.foods).map(parseFood).filter((f): f is FoodSnapshot => f !== null),
    shots: asList(raw.shots).map(parseShot).filter((s): s is ShotSnapshot => s !== null),
    strikes: asList(raw.strikes).map(parseStrike).filter((s): s is StrikeSnapshot => s !== null),
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
