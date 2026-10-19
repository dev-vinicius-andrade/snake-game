import axios from "axios";
import { computed, ref, shallowRef } from "vue";
import { resolveManagerUrl } from "@/config/env";
import { ADMIN_KEY_STORAGE_KEY, type KeyStorage } from "./useAdminRooms";

export type PerkEffect = string;

export interface PerkEffectEntry {
  effect: PerkEffect;
  amount: number;
}

export interface PerkDefinition {
  id: string;
  name: string;
  foodsRequired: number;
  /** A perk that is switched off is kept in the list but nobody gets it. */
  enabled: boolean;
  /** A Material Design Icons name such as mdi-shield. Empty: the icon of the first effect. */
  icon?: string | null;
  /** A sentence players see in the perk's tooltip. */
  description?: string | null;
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
  /** The id of a perk the snake that brings it down gets. Empty: none. */
  grantsPerk?: string | null;
  rooms: string[];
  excludedRooms: string[];
}

export interface GameplayConfig {
  perks: {
    enabled: boolean;
    shotSpreadDegrees: number;
    burstIntervalMilliseconds: number;
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

const ADMIN_KEY_HEADER = "X-Admin-Key";

function withStatus(failure: unknown): Error & { status?: number; serverMessage?: string } {
  const response = axios.isAxiosError(failure) ? failure.response : undefined;
  const serverMessage = typeof response?.data?.error === "string" ? response.data.error : undefined;
  return Object.assign(new Error(failure instanceof Error ? failure.message : "Request failed"), { status: response?.status, serverMessage });
}

const axiosHttp: GameplayHttp = {
  async get(url, adminKey) {
    try {
      return (await axios.get<GameplayPayload>(url, { headers: { [ADMIN_KEY_HEADER]: adminKey } })).data;
    } catch (failure) {
      throw withStatus(failure);
    }
  },
  async put(url, adminKey, body) {
    try {
      return (await axios.put<GameplayDocument>(url, body, { headers: { [ADMIN_KEY_HEADER]: adminKey } })).data;
    } catch (failure) {
      throw withStatus(failure);
    }
  },
  async checkScript(url, adminKey, script) {
    try {
      const reply = await axios.post<ScriptCheck>(url, script, { headers: { [ADMIN_KEY_HEADER]: adminKey, "Content-Type": "text/plain" } });
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
  minIntervalSeconds: 180,
  maxIntervalSeconds: 420,
  lifetimeSeconds: 90,
  hitPoints: 40,
  size: 60,
  fireIntervalMilliseconds: 900,
  arms: 6,
  spinDegrees: 11,
  shotSpeed: 0.5,
  minPlayers: 2,
  rewardMass: 60,
  rooms: [],
  excludedRooms: [],
};

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
    if (code === 401) {
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
      perk.effects ??= [];
      perk.rooms ??= [];
      perk.excludedRooms ??= [];
    }
    completePrizes(copy);
    copy.rooms ??= { emptyPrivateRoomMinutes: 10, maxPrivateRoomMinutes: 0 };
    const events = (copy.worldEvents ??= {
      enabled: true,
      lightning: clone(DEFAULT_LIGHTNING),
      meteors: clone(DEFAULT_METEORS),
      turret: clone(DEFAULT_TURRET),
    });
    events.enabled ??= true;
    // Lightning from before storms had no storm: it is shown with the storm defaults, which is what saving it makes it.
    events.lightning = { ...DEFAULT_LIGHTNING, ...events.lightning };
    events.meteors = { ...DEFAULT_METEORS, ...events.meteors };
    events.turret = { ...DEFAULT_TURRET, ...events.turret };
    for (const event of [events.lightning, events.meteors, events.turret]) {
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
    old.prizes = old.prizes.map((kind) => ({ ...DEFAULT_PRIZE, ...kind, rooms: kind.rooms ?? [], excludedRooms: kind.excludedRooms ?? [] }));
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
      perk.rooms = perk.rooms.map((entry) => entry.trim()).filter((entry) => entry.length > 0);
      perk.excludedRooms = perk.excludedRooms.map((entry) => entry.trim()).filter((entry) => entry.length > 0);
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
    }
    for (const event of [body.worldEvents.lightning, body.worldEvents.meteors, body.worldEvents.turret]) {
      event.rooms = event.rooms.map((entry) => entry.trim()).filter((entry) => entry.length > 0);
      event.excludedRooms = event.excludedRooms.map((entry) => entry.trim()).filter((entry) => entry.length > 0);
    }
    const turret = body.worldEvents.turret;
    if (!turret.grantsPerk?.trim()) delete turret.grantsPerk;
    else turret.grantsPerk = turret.grantsPerk.trim();
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
    perks.push({ id: freeId(perks), name: "New perk", foodsRequired: 10, enabled: true, effects: [newEffect()], rooms: [], excludedRooms: [] });
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

  return { status, error, draft, saved, effects, revision, dirty, load, save, useDefaults, revert, addPerk, removePerk, addEffect, removeEffect, addScript, removeScript, checkScript, addPrize, removePrize, perkChoices };
}
