import type {
  AbilitySnapshot,
  FoodSnapshot,
  GameColor,
  GamePoint,
  BossSnapshot,
  BountySnapshot,
  GameSnapshot,
  LeaderboardEntry,
  MazeSnapshot,
  MineSnapshot,
  RoomDimensions,
  ShotSnapshot,
  SnakeDirection,
  SnakeSnapshot,
  StrikeSnapshot,
  ZoneSnapshot,
} from "@/types/game";
import { MAX_CELLS, MAX_CORRIDOR, MIN_CELLS, MIN_CORRIDOR } from "./maze";

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

/** The tier of each perk: one for each, the first when it says nothing or something that is not a tier from 1 to 5. */
function parseTiers(raw: unknown, count: number): number[] {
  const sent = asList(raw);
  return Array.from({ length: count }, (_, index) => {
    const tier = sent[index];
    return isFinite(tier) && Number.isInteger(tier) && tier >= 1 && tier <= 5 ? tier : 1;
  });
}

/** The cooldown perks: one per perk id, with the times that are not a whole number of milliseconds from zero up made safe. */
function parseAbilities(raw: unknown): AbilitySnapshot[] | undefined {
  const abilities = asList(raw)
    .filter(isDictionary)
    .flatMap((entry) =>
      typeof entry.id === "string"
        ? [{ id: entry.id, readyMs: isFinite(entry.r) ? Math.max(0, entry.r) : 0, activeMs: isFinite(entry.a) ? Math.max(0, entry.a) : 0 }]
        : []
    );
  return abilities.length > 0 ? abilities : undefined;
}

function parseSnake(player: unknown): SnakeSnapshot | null {
  if (!isDictionary(player)) return null;
  const path = parsePath(player.path);
  if (path.length === 0) return null;
  const perks = asList(player.perks).filter((perk): perk is string => typeof perk === "string");
  return {
    guid: asString(player.guid) ?? "",
    connectionId: asString(player.id) ?? "",
    name: asString(player.name) ?? "",
    head: path[path.length - 1],
    path,
    segmentHealth: alignHealth(player.health, path.length),
    isAlive: player.dead !== true,
    isBoosting: player.boost === true,
    perks,
    perkTiers: parseTiers(player.pt, perks.length),
    autoFire: player.autoFire === true,
    foodsEaten: isFinite(player.foodsEaten) ? player.foodsEaten : 0,
    score: isFinite(player.score) ? player.score : 0,
    color: parseSingleColor(player.color),
    direction: parseDirection(player.direction),
    lives: isFinite(player.lives) && player.lives > 0 ? Math.floor(player.lives) : 0,
    protectedMs: isFinite(player.prot) && player.prot > 0 ? player.prot : 0,
    abilities: parseAbilities(player.ab),
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

/** A strike warning: where, how far it reaches (`r`), the milliseconds left (`t`) and what it is (`k`: 1 a meteor, else lightning). Like food, the place is its identity. */
function parseStrike(strike: unknown): StrikeSnapshot | null {
  if (!isDictionary(strike) || !isFinite(strike.x) || !isFinite(strike.y) || !isFinite(strike.r) || strike.r <= 0) return null;
  return {
    guid: `${strike.x},${strike.y}`,
    kind: strike.k === 1 ? "meteor" : "lightning",
    position: { x: strike.x, y: strike.y },
    radius: strike.r,
    msLeft: isFinite(strike.t) ? Math.max(0, strike.t) : 0,
  };
}

/** The boss: where (`x`, `y`), how big (`s`), the percent of it left (`hp`), where its arms point in degrees (`a`) and how many (`n`). */
function parseBoss(boss: unknown): BossSnapshot | null {
  if (!isDictionary(boss) || !isFinite(boss.x) || !isFinite(boss.y) || !isFinite(boss.s) || boss.s <= 0) return null;
  return {
    guid: asString(boss.guid) ?? "",
    position: { x: boss.x, y: boss.y },
    size: boss.s,
    hpRatio: isFinite(boss.hp) ? clamp01(boss.hp / 100) : 1,
    angle: isFinite(boss.a) ? (boss.a * Math.PI) / 180 : 0,
    arms: isFinite(boss.n) && boss.n >= 1 ? Math.floor(boss.n) : 1,
  };
}

const ZONE_PHASES = ["warning", "closing", "holding"] as const;

/** The safe zone: where (`x`, `y`), how far it reaches now (`r`) and at the end (`f`), the phase (`p`: 0, 1, 2) and the time left in it (`t`). */
function parseZone(zone: unknown): ZoneSnapshot | null {
  if (!isDictionary(zone) || !isFinite(zone.x) || !isFinite(zone.y) || !isFinite(zone.r) || zone.r <= 0) return null;
  return {
    position: { x: zone.x, y: zone.y },
    radius: zone.r,
    finalRadius: isFinite(zone.f) && zone.f > 0 ? zone.f : zone.r,
    phase: ZONE_PHASES[isFinite(zone.p) ? Math.floor(zone.p) : 0] ?? "warning",
    msLeft: isFinite(zone.t) ? Math.max(0, zone.t) : 0,
  };
}

/** A mine: where (`x`, `y`), how far its blast reaches (`r`) and the milliseconds until it is armed (`t`, 0 armed). Like food, the place is its identity. */
function parseMine(mine: unknown): MineSnapshot | null {
  if (!isDictionary(mine) || !isFinite(mine.x) || !isFinite(mine.y) || !isFinite(mine.r) || mine.r <= 0) return null;
  return {
    guid: `${mine.x},${mine.y}`,
    position: { x: mine.x, y: mine.y },
    radius: mine.r,
    msToArm: isFinite(mine.t) ? Math.max(0, mine.t) : 0,
  };
}

/** The bounty: the guid of the wanted snake, the milliseconds left (`t`) and the reward mass (`m`). */
function parseBounty(bounty: unknown): BountySnapshot | null {
  if (!isDictionary(bounty)) return null;
  const guid = asString(bounty.guid);
  if (!guid) return null;
  return {
    guid,
    msLeft: isFinite(bounty.t) ? Math.max(0, bounty.t) : 0,
    rewardMass: isFinite(bounty.m) ? Math.max(0, bounty.m) : 0,
  };
}

/**
 * The maze: the corner (`x`, `y`), the cells (`c` across, `r` down), the blocks across a corridor (`k`), the twistiness (`v`), the seed
 * (`s`), the size of a block (`u`) and the milliseconds left (`t`). Null when it cannot be built: the numbers must be ones the generator accepts.
 */
function parseMaze(maze: unknown): MazeSnapshot | null {
  if (!isDictionary(maze) || !isFinite(maze.x) || !isFinite(maze.y) || !isFinite(maze.s) || !isFinite(maze.u) || maze.u <= 0) return null;
  const whole = (value: unknown, min: number, max: number): value is number => isFinite(value) && Number.isInteger(value) && value >= min && value <= max;
  if (!whole(maze.c, MIN_CELLS, MAX_CELLS) || !whole(maze.r, MIN_CELLS, MAX_CELLS)) return null;
  if (!whole(maze.k, MIN_CORRIDOR, MAX_CORRIDOR) || !whole(maze.v, 0, 100) || !whole(maze.s, 0, 4294967295)) return null;
  return {
    position: { x: maze.x, y: maze.y },
    columns: maze.c,
    rows: maze.r,
    corridor: maze.k,
    twistiness: maze.v,
    seed: maze.s,
    unit: maze.u,
    msLeft: isFinite(maze.t) ? Math.max(0, maze.t) : 0,
  };
}

/** The milliseconds left of an event that is only a countdown (a fruit shower, a sugar rush); 0 when there is none. */
const msLeftOf = (value: unknown): number => (isFinite(value) && value > 0 ? value : 0);

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
    boss: parseBoss(raw.boss),
    zone: parseZone(raw.zone),
    mines: asList(raw.mines).map(parseMine).filter((m): m is MineSnapshot => m !== null),
    bounty: parseBounty(raw.bounty),
    showerMsLeft: msLeftOf(raw.shower),
    rushMsLeft: msLeftOf(raw.rush),
    maze: parseMaze(raw.maze),
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
