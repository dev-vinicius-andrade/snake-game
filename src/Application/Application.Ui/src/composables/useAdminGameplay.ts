import axios from "axios";
import { computed, ref, shallowRef } from "vue";
import { resolveManagerUrl } from "@/config/env";
import { ADMIN_KEY_STORAGE_KEY, type KeyStorage } from "./useAdminRooms";

export type PerkEffect = string;

export interface PerkDefinition {
  id: string;
  name: string;
  foodsRequired: number;
  effect: PerkEffect;
  amount: number;
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
};

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

  function take(document: GameplayDocument) {
    revision.value = document.revision;
    saved.value = clone(document.config);
    draft.value = clone(document.config);
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
    try {
      take(await http.put(url, adminKey, body));
      status.value = "saved";
    } catch (failure) {
      failWith(failure);
    }
  }

  function useDefaults() {
    if (defaults.value) draft.value = clone(defaults.value);
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
    perks.push({ id: freeId(perks), name: "New perk", foodsRequired: 10, effect: effects.value[0] ?? "ExtraShots", amount: 1 });
  }

  function removePerk(index: number) {
    draft.value?.perks.perks.splice(index, 1);
  }

  return { status, error, draft, saved, effects, revision, dirty, load, save, useDefaults, revert, addPerk, removePerk };
}
