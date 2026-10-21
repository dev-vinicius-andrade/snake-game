import { adminHeaders, isRejected } from "./adminAuth";
import axios from "axios";
import { computed, ref, shallowRef } from "vue";
import { resolveManagerUrl } from "@/config/env";
import { isAbilityEffect } from "@/game/perkInfo";
import { ADMIN_KEY_STORAGE_KEY, type KeyStorage } from "./useAdminRooms";

export type PerkEffect = string;

export interface PerkEffectEntry {
  effect: PerkEffect;
  amount: number;
  /** For event resistance: the one event it is for (lightning, meteors, mines, zone). Left out: all of them. */
  target?: string | null;
}

/**
 * What else a prize, the turret, a bounty or the maze prize gives: a random perk, extra lives, a tier surge (a perk the snake has lifted by
 * some tiers beyond what its foods give), mass, a mend. Everything off gives nothing.
 */
export interface RewardConfig {
  grantsRandomPerk: boolean;
  /** Lives given, 0 to 5. */
  extraLives: number;
  /** Tiers a perk is lifted by, picked between the two. 0 and 0: no surge. 0 to 10. */
  tierSurgeMin: number;
  tierSurgeMax: number;
  /** The perk to lift. Empty: a random one of those the snake has that can still go up. */
  tierSurgePerk?: string | null;
  /** Mass given at once, 0 to 10000. */
  bonusMass: number;
  /** Heals the whole body. */
  mend: boolean;
}

/** Nothing given. What a reward is when a config from before rewards says nothing. */
export const EMPTY_REWARD: RewardConfig = { grantsRandomPerk: false, extraLives: 0, tierSurgeMin: 0, tierSurgeMax: 0, bonusMass: 0, mend: false };

const completeReward = (reward?: Partial<RewardConfig> | null): RewardConfig => ({ ...EMPTY_REWARD, ...reward });

/** A reward as it is sent: a blank perk to lift is left out. */
function cleanReward(reward: RewardConfig): RewardConfig {
  const copy = { ...reward };
  if (!copy.tierSurgePerk?.trim()) delete copy.tierSurgePerk;
  else copy.tierSurgePerk = copy.tierSurgePerk.trim();
  return copy;
}

/** Whether the perk has a cooldown ability (shield burst, dash, heal pulse, vacuum burst), so it needs a cooldown. */
export const hasAbility = (perk: Pick<PerkDefinition, "effects">): boolean => perk.effects.some((entry) => isAbilityEffect(entry.effect));

/** A step up of a perk: with this many foods everything it does is `multiplier` times its amounts. */
export interface PerkTierConfig {
  foodsRequired: number;
  multiplier: number;
  /** A tier eating never reaches: only a reward's tier surge lifts a perk into it. It must come after every eatable tier. */
  rewardOnly?: boolean;
}

/** Whether a reward-only tier has an eatable one after it (tiers are ordered by foods), which the server refuses. */
export function rewardOnlyOutOfPlace(tiers: PerkTierConfig[] | undefined): boolean {
  const ordered = [...(tiers ?? [])].sort((first, second) => first.foodsRequired - second.foodsRequired);
  const first = ordered.findIndex((tier) => tier.rewardOnly);
  return first >= 0 && ordered.slice(first).some((tier) => !tier.rewardOnly);
}

export interface PerkDefinition {
  id: string;
  name: string;
  foodsRequired: number;
  /** A perk that is switched off is kept in the list but nobody gets it. */
  enabled: boolean;
  /** The steps up after the first tier (the perk as it is), each asking for more foods. At most four. */
  tiers?: PerkTierConfig[];
  /** A Material Design Icons name such as mdi-shield. Empty: the icon of the first effect. */
  icon?: string | null;
  /** A sentence players see in the perk's tooltip. */
  description?: string | null;
  /** Seconds a cooldown ability of the perk (shield burst, dash, heal pulse, vacuum burst) takes to recharge. 0 for a perk with none. */
  cooldownSeconds?: number;
  /** Seconds a shield burst, dash or vacuum burst lasts. */
  durationSeconds?: number;
  /** Everything the perk does. Empty only for a perk whose script does it. */
  effects: PerkEffectEntry[];
  /** Invite codes, names or ids of the rooms the perk works in. Empty: every room (a perk with a script: none). */
  rooms: string[];
  /** Rooms the perk does not work in, even where `rooms` would allow it. */
  excludedRooms: string[];
  /** JavaScript for logic the effects cannot say. The perk is off everywhere until it names rooms. */
  script?: string;
}

/** What every event that comes as a burst of strikes has: how often, how big, how well signalled, and where. */
export interface StormConfigBase {
  enabled: boolean;
  minIntervalSeconds: number;
  maxIntervalSeconds: number;
  /** World units around a spot that a strike reaches. */
  radius: number;
  /** Milliseconds each spot is shown before it is struck. */
  telegraphMilliseconds: number;
  /** Share (0..1) of strikes aimed near a random living snake; 0 is all random. */
  aimedShare: number;
  rooms: string[];
  excludedRooms: string[];
}

/** Lightning storms: a burst of small strikes, each marked on the map a moment before it falls. */
export interface LightningConfig extends StormConfigBase {
  minStrikes: number;
  maxStrikes: number;
  /** Seconds over which the strikes of a storm begin. */
  stormSeconds: number;
}

/** Meteor rain: a shower of meteors, each shown by a big shadow before it lands. */
export interface MeteorConfig extends StormConfigBase {
  minMeteors: number;
  maxMeteors: number;
  /** Seconds over which the meteors of a rain begin to fall. */
  rainSeconds: number;
}

/** The turret: a mini boss that now and then appears, fires volleys that turn round it, and is shot down for a reward. */
export interface TurretConfig {
  enabled: boolean;
  minIntervalSeconds: number;
  maxIntervalSeconds: number;
  /** Seconds it stays when nobody brings it down. */
  lifetimeSeconds: number;
  /** Shots it takes to bring it down. */
  hitPoints: number;
  /** Side of its square, in world units. */
  size: number;
  fireIntervalMilliseconds: number;
  /** Shots in a volley, evenly spread. */
  arms: number;
  /** Degrees the volley turns by every time it fires. */
  spinDegrees: number;
  /** How fast its shots fly compared with a snake shot (1). */
  shotSpeed: number;
  minPlayers: number;
  /** Mass that falls as food where it is brought down. */
  rewardMass: number;
  /** How many foods bringing it down counts for toward the perks (and tiers) of the snake that does it. */
  foodsCredit: number;
  /** The id of a perk the snake that brings it down gets. Empty: none. */
  grantsPerk?: string | null;
  /** What else the snake that brings it down gets. */
  reward: RewardConfig;
  rooms: string[];
  excludedRooms: string[];
}

/** The shrinking safe zone: a circle that closes in, with snakes outside it losing mass and dying. */
export interface ZoneConfig {
  enabled: boolean;
  minIntervalSeconds: number;
  maxIntervalSeconds: number;
  /** Seconds the safe spot is shown before the zone starts to close, with nobody harmed. */
  warningSeconds: number;
  /** Seconds it takes to close from covering the whole room to its smallest. */
  shrinkSeconds: number;
  /** Seconds it stays at its smallest. */
  holdSeconds: number;
  /** World units around the safe spot that stay safe at the end. */
  finalRadius: number;
  damageIntervalMilliseconds: number;
  /** Mass a snake outside loses each time; it falls as food. A snake with none left dies. */
  damageMass: number;
  minPlayers: number;
  rooms: string[];
  excludedRooms: string[];
}

/** What every world event has: whether it is on, how often it comes, how many players it needs, and where. */
export interface WorldEventConfigBase {
  enabled: boolean;
  minIntervalSeconds: number;
  maxIntervalSeconds: number;
  minPlayers: number;
  rooms: string[];
  excludedRooms: string[];
}

/** The fruit shower: for a while fruit falls all over the room. */
export interface FruitShowerConfig extends WorldEventConfigBase {
  /** Seconds it lasts, 1 to 60. */
  durationSeconds: number;
  /** Fruits that fall each second, 1 to 30. */
  fruitsPerSecond: number;
}

/** The bounty: a price on one snake's head, shown to everyone, for whoever kills it in time. */
export interface BountyConfig extends WorldEventConfigBase {
  /** Seconds it lasts, 5 to 3600. */
  durationSeconds: number;
  /** Mass the snake that kills the wanted one gets, 0 to 10000. */
  rewardMass: number;
  /** How many foods the kill counts for toward perks (and tiers), 0 to 1000. */
  foodsCredit: number;
  /** What else the snake that kills the wanted one gets. */
  reward: RewardConfig;
}

/** Mines: laid round the room, each armed after a moment, they stay until a snake touches one and it blows up. */
export interface MinesConfig extends WorldEventConfigBase {
  /** Mines laid each time, 1 to 40. */
  minMines: number;
  maxMines: number;
  /** World units the blast reaches, 1 to 1000. */
  radius: number;
  /** World units from a mine at which a snake sets it off, 1 to 200. */
  triggerRadius: number;
  /** Milliseconds before a mine is armed, 0 to 60000. */
  armMilliseconds: number;
}

/** The sugar rush: for a while boosting costs no mass. */
export interface SugarRushConfig extends WorldEventConfigBase {
  /** Seconds it lasts, 1 to 600. */
  durationSeconds: number;
}

/** The maze: for a while a maze stands in a part of the room, with a big prize at the end. Sizes are in head sizes. */
export interface MazeConfig extends WorldEventConfigBase {
  /** Seconds it stands when nobody takes the prize, 10 to 3600. */
  durationSeconds: number;
  /** Cells across and down, 3 to 30. The maze is made smaller when the room cannot hold it. */
  columns: number;
  rows: number;
  /** Head sizes across a corridor, 3 to 12. A wall is one head size thick. */
  corridorHeads: number;
  /** 0 (long straight halls) to 100 (turns at every chance). */
  twistiness: number;
  /** Mass a snake loses when its head runs into a wall; it never dies of it. 0 to 1000. */
  wallMassLoss: number;
  /** Percent of the cells that get a fruit, 0 to 100. */
  foodPercent: number;
  /** Mass the snake that takes the prize gains, 1 to 10000. */
  prizeMass: number;
  /** How many foods taking the prize counts for toward perks, 0 to 1000. */
  prizeFoodsCredit: number;
  /** A perk of the list the snake that takes the prize gets. Left out: none. */
  prizeGrantsPerk?: string | null;
  /** What else the snake that takes the prize gets. */
  prizeReward: RewardConfig;
  /** Six hex digits such as #ffd23f. */
  prizeColor: string;
}

export interface GameplayConfig {
  perks: {
    enabled: boolean;
    shotSpreadDegrees: number;
    burstIntervalMilliseconds: number;
    /** How many foods killing another snake counts for toward perks and their tiers. */
    killFoodsCredit: number;
    /** Share (0..1) of its mass a snake keeps when an extra life saves it. */
    extraLifeKeepShare: number;
    /** Seconds a snake that an extra life saved cannot be hurt. */
    extraLifeProtectSeconds: number;
    perks: PerkDefinition[];
  };
  prize: {
    /** Switches every kind of prize off at once. */
    enabled: boolean;
    prizes: PrizeDefinition[];
  };
  /** How long private rooms live. */
  rooms: {
    /** Minutes a private room may stay empty before it is closed. */
    emptyPrivateRoomMinutes: number;
    /** Minutes after which a private room is closed whoever is in it; 0 is no limit. */
    maxPrivateRoomMinutes: number;
  };
  /** Things that happen to a whole room now and then. */
  worldEvents: {
    /** Switches every world event off at once. */
    enabled: boolean;
    lightning: LightningConfig;
    meteors: MeteorConfig;
    turret: TurretConfig;
    zone: ZoneConfig;
    fruitShower: FruitShowerConfig;
    bounty: BountyConfig;
    mines: MinesConfig;
    sugarRush: SugarRushConfig;
    maze: MazeConfig;
  };
}

/** One kind of prize. Each has its own timing, and can give a perk to the snake that eats it. */
export interface PrizeDefinition {
  id: string;
  name: string;
  enabled: boolean;
  spawnIntervalSeconds: number;
  lifetimeSeconds: number;
  value: number;
  foodsCredit: number;
  minPlayers: number;
  /** The id of a perk the snake gets, whatever the foods it has eaten. Empty: none. */
  grantsPerk?: string | null;
  /** What else the snake that eats it gets: a random perk, lives, a tier surge, mass, a mend. */
  reward: RewardConfig;
  /** Six hex digits, for example #ffd23f. */
  color: string;
  rooms: string[];
  excludedRooms: string[];
}

export interface GameplayDocument {
  revision: string;
  config: GameplayConfig;
}

export interface GameplayPayload {
  current: GameplayDocument;
  defaults: GameplayConfig;
  effects: string[];
}

/** A failure has the HTTP status in `status`, and what the server said in `serverMessage` when it said something. */
export interface GameplayHttp {
  get(url: string, adminKey: string): Promise<GameplayPayload>;
  put(url: string, adminKey: string, body: GameplayConfig): Promise<GameplayDocument>;
  /** Asks whether a script would be accepted. Nothing is saved. */
  checkScript(url: string, adminKey: string, script: string): Promise<ScriptCheck>;
}

export interface ScriptCheck {
  ok: boolean;
  error: string | null;
}

export type GameplayStatus = "idle" | "loading" | "ready" | "saved" | "signedOut" | "error";


function withStatus(failure: unknown): Error & { status?: number; serverMessage?: string } {
  const response = axios.isAxiosError(failure) ? failure.response : undefined;
  const serverMessage = typeof response?.data?.error === "string" ? response.data.error : undefined;
  return Object.assign(new Error(failure instanceof Error ? failure.message : "Request failed"), { status: response?.status, serverMessage });
}

const axiosHttp: GameplayHttp = {
  async get(url, adminKey) {
    try {
      return (await axios.get<GameplayPayload>(url, { headers: adminHeaders(adminKey) })).data;
    } catch (failure) {
      throw withStatus(failure);
    }
  },
  async put(url, adminKey, body) {
    try {
      return (await axios.put<GameplayDocument>(url, body, { headers: adminHeaders(adminKey) })).data;
    } catch (failure) {
      throw withStatus(failure);
    }
  },
  async checkScript(url, adminKey, script) {
    try {
      const reply = await axios.post<ScriptCheck>(url, script, { headers: { ...adminHeaders(adminKey), "Content-Type": "text/plain" } });
      return reply.data;
    } catch (failure) {
      throw withStatus(failure);
    }
  },
};

/** What lightning starts as, and what a server that does not send it yet gets. */
const DEFAULT_LIGHTNING: LightningConfig = {
  enabled: true,
  minIntervalSeconds: 45,
  maxIntervalSeconds: 120,
  radius: 90,
  telegraphMilliseconds: 1500,
  minStrikes: 5,
  maxStrikes: 12,
  stormSeconds: 6,
  aimedShare: 0.25,
  rooms: [],
  excludedRooms: [],
};

/** What meteor rain starts as, and what a server that does not send it yet gets. */
const DEFAULT_METEORS: MeteorConfig = {
  enabled: true,
  minIntervalSeconds: 120,
  maxIntervalSeconds: 300,
  radius: 130,
  telegraphMilliseconds: 2000,
  minMeteors: 6,
  maxMeteors: 14,
  rainSeconds: 8,
  aimedShare: 0.2,
  rooms: [],
  excludedRooms: [],
};

/** What the turret starts as, and what a server that does not send it yet gets. */
const DEFAULT_TURRET: TurretConfig = {
  enabled: true,
  minIntervalSeconds: 120,
  maxIntervalSeconds: 300,
  lifetimeSeconds: 90,
  hitPoints: 40,
  size: 60,
  fireIntervalMilliseconds: 900,
  arms: 6,
  spinDegrees: 11,
  shotSpeed: 0.5,
  minPlayers: 1,
  rewardMass: 60,
  foodsCredit: 15,
  reward: { ...EMPTY_REWARD },
  rooms: [],
  excludedRooms: [],
};

/** What the safe zone starts as, and what a server that does not send it yet gets. */
const DEFAULT_ZONE: ZoneConfig = {
  enabled: true,
  minIntervalSeconds: 300,
  maxIntervalSeconds: 600,
  warningSeconds: 15,
  shrinkSeconds: 60,
  holdSeconds: 30,
  finalRadius: 160,
  damageIntervalMilliseconds: 1000,
  damageMass: 4,
  minPlayers: 2,
  rooms: [],
  excludedRooms: [],
};

/** What the fruit shower starts as, and what a config saved before it gets. */
const DEFAULT_FRUIT_SHOWER: FruitShowerConfig = {
  enabled: true,
  minIntervalSeconds: 180,
  maxIntervalSeconds: 420,
  durationSeconds: 20,
  fruitsPerSecond: 8,
  minPlayers: 1,
  rooms: [],
  excludedRooms: [],
};

/** What the bounty starts as, and what a config saved before it gets. */
const DEFAULT_BOUNTY: BountyConfig = {
  enabled: true,
  minIntervalSeconds: 240,
  maxIntervalSeconds: 600,
  durationSeconds: 90,
  rewardMass: 150,
  foodsCredit: 10,
  reward: { ...EMPTY_REWARD },
  minPlayers: 3,
  rooms: [],
  excludedRooms: [],
};

/** What mines start as, and what a config saved before them gets. */
const DEFAULT_MINES: MinesConfig = {
  enabled: true,
  minIntervalSeconds: 150,
  maxIntervalSeconds: 360,
  minMines: 6,
  maxMines: 14,
  radius: 60,
  triggerRadius: 20,
  armMilliseconds: 2000,
  minPlayers: 1,
  rooms: [],
  excludedRooms: [],
};

/** What the sugar rush starts as, and what a config saved before it gets. */
const DEFAULT_SUGAR_RUSH: SugarRushConfig = {
  enabled: true,
  minIntervalSeconds: 240,
  maxIntervalSeconds: 600,
  durationSeconds: 30,
  minPlayers: 1,
  rooms: [],
  excludedRooms: [],
};

/** What the maze starts as, and what a config saved before it gets. */
const DEFAULT_MAZE: MazeConfig = {
  enabled: true,
  minIntervalSeconds: 420,
  maxIntervalSeconds: 900,
  durationSeconds: 240,
  columns: 12,
  rows: 8,
  corridorHeads: 4,
  twistiness: 60,
  wallMassLoss: 2,
  foodPercent: 25,
  minPlayers: 2,
  prizeMass: 200,
  prizeFoodsCredit: 20,
  prizeReward: { ...EMPTY_REWARD },
  prizeColor: "#ffd23f",
  rooms: [],
  excludedRooms: [],
};

/** Every world event of a config, each with the rooms it is in and left out of. */
const eventsOf = (events: GameplayConfig["worldEvents"]) => [
  events.lightning,
  events.meteors,
  events.turret,
  events.zone,
  events.fruitShower,
  events.bounty,
  events.mines,
  events.sugarRush,
  events.maze,
];

/** What a new kind of prize starts as. */
const DEFAULT_PRIZE: PrizeDefinition = {
  id: "prize",
  name: "Prize",
  enabled: true,
  spawnIntervalSeconds: 60,
  lifetimeSeconds: 45,
  value: 20,
  foodsCredit: 8,
  minPlayers: 2,
  reward: { ...EMPTY_REWARD },
  color: "#ffd23f",
  rooms: [],
  excludedRooms: [],
};

/** What a new script starts from: both functions, each doing something small, so it is clear what they are for. */
export const SCRIPT_EXAMPLE = `// Called a few times a second for each snake that has this perk. Return the extra effects.
function modify(snake, room) {
  return {
    extraShots: snake.length > 40 ? 1 : 0,
  };
}

// Called each time the snake eats. Return extra mass or healing.
function onEat(snake, food, room) {
  return { heal: food.isPrize ? 30 : 0 };
}
`;

export interface UseAdminGameplayOptions {
  managerUrl?: string;
  http?: GameplayHttp;
  /** Where the admin key is kept; the one the rooms page signed in with. */
  storage?: KeyStorage | null;
}

function defaultStorage(): KeyStorage | null {
  try {
    return typeof sessionStorage === "undefined" ? null : sessionStorage;
  } catch {
    return null;
  }
}

const clone = <T>(value: T): T => JSON.parse(JSON.stringify(value));

/** The administrator's editor for perks and the prize: load the config, edit a draft of it, save it. */
export function useAdminGameplay(options: UseAdminGameplayOptions = {}) {
  const managerUrl = (options.managerUrl ?? resolveManagerUrl()).replace(/\/+$/, "");
  const http = options.http ?? axiosHttp;
  const storage = options.storage === undefined ? defaultStorage() : options.storage;
  const url = `${managerUrl}/admin/gameplay`;

  const status = shallowRef<GameplayStatus>("idle");
  /** `unavailable` or `invalidKey`, or the reason the server gave for refusing a config. */
  const error = shallowRef<string | null>(null);
  const saved = ref<GameplayConfig | null>(null);
  const draft = ref<GameplayConfig | null>(null);
  const defaults = ref<GameplayConfig | null>(null);
  const effects = shallowRef<string[]>([]);
  const revision = shallowRef<string | null>(null);

  const dirty = computed(() => JSON.stringify(draft.value) !== JSON.stringify(saved.value));

  function key(): string | null {
    try {
      return storage?.getItem(ADMIN_KEY_STORAGE_KEY) || null;
    } catch {
      return null;
    }
  }

  function failWith(failure: unknown) {
    const code = (failure as { status?: number }).status;
    if (isRejected(code)) {
      status.value = "signedOut";
      error.value = "invalidKey";
      return;
    }
    status.value = "error";
    error.value = code === 400 ? ((failure as { serverMessage?: string }).serverMessage ?? "unavailable") : "unavailable";
  }

  /** A server that does not know a field yet sends none: lists are then empty, switches on, and a single old prize becomes a kind. */
  function complete(config: GameplayConfig): GameplayConfig {
    const copy = clone(config);
    for (const perk of copy.perks.perks) {
      perk.enabled ??= true;
      perk.tiers = (perk.tiers ?? []).map((tier) => ({ ...tier, rewardOnly: tier.rewardOnly ?? false }));
      perk.effects ??= [];
      perk.rooms ??= [];
      perk.excludedRooms ??= [];
      perk.cooldownSeconds ??= 0;
      perk.durationSeconds ??= 0;
    }
    copy.perks.killFoodsCredit ??= 3;
    copy.perks.extraLifeKeepShare ??= 0.5;
    copy.perks.extraLifeProtectSeconds ??= 3;
    completePrizes(copy);
    copy.rooms ??= { emptyPrivateRoomMinutes: 10, maxPrivateRoomMinutes: 0 };
    const events = (copy.worldEvents ??= {
      enabled: true,
      lightning: clone(DEFAULT_LIGHTNING),
      meteors: clone(DEFAULT_METEORS),
      turret: clone(DEFAULT_TURRET),
      zone: clone(DEFAULT_ZONE),
      fruitShower: clone(DEFAULT_FRUIT_SHOWER),
      bounty: clone(DEFAULT_BOUNTY),
      mines: clone(DEFAULT_MINES),
      sugarRush: clone(DEFAULT_SUGAR_RUSH),
      maze: clone(DEFAULT_MAZE),
    });
    events.enabled ??= true;
    // Lightning from before storms had no storm: it is shown with the storm defaults, which is what saving it makes it.
    events.lightning = { ...DEFAULT_LIGHTNING, ...events.lightning };
    events.meteors = { ...DEFAULT_METEORS, ...events.meteors };
    events.turret = { ...DEFAULT_TURRET, ...events.turret };
    events.zone = { ...DEFAULT_ZONE, ...events.zone };
    // Configs saved before these events have none of them: they come in with their defaults, and saving sends them.
    events.fruitShower = { ...DEFAULT_FRUIT_SHOWER, ...events.fruitShower };
    events.bounty = { ...DEFAULT_BOUNTY, ...events.bounty };
    events.mines = { ...DEFAULT_MINES, ...events.mines };
    events.sugarRush = { ...DEFAULT_SUGAR_RUSH, ...events.sugarRush };
    events.maze = { ...DEFAULT_MAZE, ...events.maze };
    events.turret.reward = completeReward(events.turret.reward);
    events.bounty.reward = completeReward(events.bounty.reward);
    events.maze.prizeReward = completeReward(events.maze.prizeReward);
    for (const event of eventsOf(events)) {
      event.rooms ??= [];
      event.excludedRooms ??= [];
    }
    return copy;
  }

  function completePrizes(copy: GameplayConfig) {
    const old = copy.prize as GameplayConfig["prize"] & Partial<Omit<PrizeDefinition, "id" | "name" | "enabled">>;
    if (!Array.isArray(old.prizes)) {
      old.prizes = [
        {
          ...DEFAULT_PRIZE,
          id: "prize",
          name: "Prize",
          spawnIntervalSeconds: old.spawnIntervalSeconds ?? DEFAULT_PRIZE.spawnIntervalSeconds,
          lifetimeSeconds: old.lifetimeSeconds ?? DEFAULT_PRIZE.lifetimeSeconds,
          value: old.value ?? DEFAULT_PRIZE.value,
          foodsCredit: old.foodsCredit ?? DEFAULT_PRIZE.foodsCredit,
          minPlayers: old.minPlayers ?? DEFAULT_PRIZE.minPlayers,
        },
      ];
    }
    for (const field of ["spawnIntervalSeconds", "lifetimeSeconds", "value", "foodsCredit", "minPlayers"] as const) delete old[field];
    old.prizes = old.prizes.map((kind) => ({
      ...DEFAULT_PRIZE,
      ...kind,
      reward: completeReward(kind.reward),
      rooms: kind.rooms ?? [],
      excludedRooms: kind.excludedRooms ?? [],
    }));
  }

  function take(document: GameplayDocument) {
    revision.value = document.revision;
    saved.value = complete(document.config);
    draft.value = complete(document.config);
  }

  async function load() {
    const adminKey = key();
    if (!adminKey) return;
    status.value = "loading";
    error.value = null;
    try {
      const payload = await http.get(url, adminKey);
      take(payload.current);
      defaults.value = clone(payload.defaults);
      effects.value = payload.effects;
      status.value = "ready";
    } catch (failure) {
      failWith(failure);
    }
  }

  async function save() {
    const adminKey = key();
    if (!adminKey || !draft.value) return;
    error.value = null;
    const body = clone(draft.value);
    body.perks.perks.sort((first, second) => first.foodsRequired - second.foodsRequired);
    for (const perk of body.perks.perks) {
      perk.tiers = [...(perk.tiers ?? [])].sort((first, second) => first.foodsRequired - second.foodsRequired).map((tier) => ({ ...tier, rewardOnly: tier.rewardOnly === true }));
      perk.rooms = perk.rooms.map((entry) => entry.trim()).filter((entry) => entry.length > 0);
      perk.excludedRooms = perk.excludedRooms.map((entry) => entry.trim()).filter((entry) => entry.length > 0);
      for (const entry of perk.effects) {
        if (entry.effect !== "EventResistance" || !entry.target?.trim()) delete entry.target;
      }
      // A perk with no cooldown ability has nothing to recharge: stale numbers would only confuse.
      if (!hasAbility(perk)) {
        perk.cooldownSeconds = 0;
        perk.durationSeconds = 0;
      }
      if (!perk.script?.trim()) delete perk.script;
      if (!perk.icon?.trim()) delete perk.icon;
      else perk.icon = perk.icon.trim();
      if (!perk.description?.trim()) delete perk.description;
      else perk.description = perk.description.trim();
    }
    for (const kind of body.prize.prizes) {
      kind.rooms = kind.rooms.map((entry) => entry.trim()).filter((entry) => entry.length > 0);
      kind.excludedRooms = kind.excludedRooms.map((entry) => entry.trim()).filter((entry) => entry.length > 0);
      if (!kind.grantsPerk?.trim()) delete kind.grantsPerk;
      kind.reward = cleanReward(kind.reward);
    }
    for (const event of eventsOf(body.worldEvents)) {
      event.rooms = event.rooms.map((entry) => entry.trim()).filter((entry) => entry.length > 0);
      event.excludedRooms = event.excludedRooms.map((entry) => entry.trim()).filter((entry) => entry.length > 0);
    }
    const turret = body.worldEvents.turret;
    if (!turret.grantsPerk?.trim()) delete turret.grantsPerk;
    else turret.grantsPerk = turret.grantsPerk.trim();
    turret.reward = cleanReward(turret.reward);
    body.worldEvents.bounty.reward = cleanReward(body.worldEvents.bounty.reward);
    const maze = body.worldEvents.maze;
    if (!maze.prizeGrantsPerk?.trim()) delete maze.prizeGrantsPerk;
    else maze.prizeGrantsPerk = maze.prizeGrantsPerk.trim();
    maze.prizeReward = cleanReward(maze.prizeReward);
    try {
      take(await http.put(url, adminKey, body));
      status.value = "saved";
    } catch (failure) {
      failWith(failure);
    }
  }

  function useDefaults() {
    if (defaults.value) draft.value = complete(defaults.value);
  }

  function revert() {
    if (saved.value) draft.value = clone(saved.value);
  }

  function freeId(perks: PerkDefinition[]): string {
    for (let number = perks.length + 1; ; number++) {
      const id = `perk-${number}`;
      if (!perks.some((perk) => perk.id === id)) return id;
    }
  }

  function addPerk() {
    if (!draft.value) return;
    const perks = draft.value.perks.perks;
    perks.push({ id: freeId(perks), name: "New perk", foodsRequired: 10, enabled: true, effects: [newEffect()], tiers: [], rooms: [], excludedRooms: [] });
  }

  function addPrize() {
    if (!draft.value) return;
    const prizes = draft.value.prize.prizes;
    let number = prizes.length + 1;
    while (prizes.some((kind) => kind.id === `prize-${number}`)) number++;
    prizes.push({ ...clone(DEFAULT_PRIZE), id: `prize-${number}`, name: "New prize" });
  }

  /** The last kind stays: a list with none would bring the default prize back. Switch the prizes off instead. */
  function removePrize(index: number) {
    const prizes = draft.value?.prize.prizes;
    if (prizes && prizes.length > 1) prizes.splice(index, 1);
  }

  /** The perks a prize can give, by id and name. */
  const perkChoices = computed(() => (draft.value?.perks.perks ?? []).map((perk) => ({ id: perk.id, name: perk.name })));

  const newEffect = (): PerkEffectEntry => ({ effect: effects.value[0] ?? "ExtraShots", amount: 1 });

  /** Most tiers a perk can have after its first. */
  const MAX_EXTRA_TIERS = 9;

  /** A tier that asks for another helping of foods and does half as much again as the one before. */
  function addTier(perkIndex: number) {
    const perk = draft.value?.perks.perks[perkIndex];
    if (!perk) return;
    const tiers = (perk.tiers ??= []);
    if (tiers.length >= MAX_EXTRA_TIERS) return;
    const last = tiers[tiers.length - 1];
    tiers.push({ foodsRequired: (last?.foodsRequired ?? perk.foodsRequired) + perk.foodsRequired, multiplier: (last?.multiplier ?? 1) + 0.5, rewardOnly: false });
  }

  function removeTier(perkIndex: number, tierIndex: number) {
    draft.value?.perks.perks[perkIndex]?.tiers?.splice(tierIndex, 1);
  }

  function addEffect(perkIndex: number) {
    draft.value?.perks.perks[perkIndex]?.effects.push(newEffect());
  }

  /** A perk keeps its last effect, unless a script is there to do the work. */
  /** Gives a perk a script, from an example, unless it has one. */
  function addScript(perkIndex: number) {
    const perk = draft.value?.perks.perks[perkIndex];
    if (perk && !perk.script?.trim()) perk.script = SCRIPT_EXAMPLE;
  }

  /** Takes the script away, unless the perk has no effect of its own: then the script is all it is. */
  function removeScript(perkIndex: number) {
    const perk = draft.value?.perks.perks[perkIndex];
    if (perk && perk.effects.length > 0) delete perk.script;
  }

  async function checkScript(script: string): Promise<ScriptCheck> {
    const adminKey = key();
    if (!adminKey || !script.trim()) return { ok: false, error: "empty" };
    try {
      return await http.checkScript(`${url}/check-script`, adminKey, script);
    } catch {
      return { ok: false, error: "unavailable" };
    }
  }

  function removeEffect(perkIndex: number, effectIndex: number) {
    const perk = draft.value?.perks.perks[perkIndex];
    if (!perk || (perk.effects.length <= 1 && !perk.script?.trim())) return;
    perk.effects.splice(effectIndex, 1);
  }

  function removePerk(index: number) {
    draft.value?.perks.perks.splice(index, 1);
  }

  return { status, error, draft, saved, effects, revision, dirty, load, save, useDefaults, revert, addPerk, removePerk, addEffect, removeEffect, addTier, removeTier, addScript, removeScript, checkScript, addPrize, removePrize, perkChoices };
}
