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
  /** Everything the perk does. Empty only for a perk whose script does it. */
  effects: PerkEffectEntry[];
  /** Invite codes, names or ids of the rooms the perk works in. Empty: every room (a perk with a script: none). */
  rooms: string[];
  /** Rooms the perk does not work in, even where `rooms` would allow it. */
  excludedRooms: string[];
  /** JavaScript for logic the effects cannot say. The perk is off everywhere until it names rooms. */
  script?: string;
}

export interface GameplayConfig {
  perks: {
    enabled: boolean;
    shotSpreadDegrees: number;
    burstIntervalMilliseconds: number;
    perks: PerkDefinition[];
  };
  prize: {
    enabled: boolean;
    spawnIntervalSeconds: number;
    lifetimeSeconds: number;
    value: number;
    foodsCredit: number;
    minPlayers: number;
  };
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

  /** A server that does not know a field yet sends none: the lists are then empty. */
  function complete(config: GameplayConfig): GameplayConfig {
    const copy = clone(config);
    for (const perk of copy.perks.perks) {
      perk.effects ??= [];
      perk.rooms ??= [];
      perk.excludedRooms ??= [];
    }
    return copy;
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
    }
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
    perks.push({ id: freeId(perks), name: "New perk", foodsRequired: 10, effects: [newEffect()], rooms: [], excludedRooms: [] });
  }

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

  return { status, error, draft, saved, effects, revision, dirty, load, save, useDefaults, revert, addPerk, removePerk, addEffect, removeEffect, addScript, removeScript, checkScript };
}
