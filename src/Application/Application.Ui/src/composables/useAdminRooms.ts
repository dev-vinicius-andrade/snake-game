import { ACCOUNT_CREDENTIAL, adminHeaders, isRejected } from "./adminAuth";
import axios from "axios";
import { shallowRef } from "vue";
import { resolveManagerUrl } from "@/config/env";

export interface AdminRoom {
  id: string;
  code: string | null;
  name: string | null;
  isPrivate: boolean;
  players: number;
  maxPlayers: number;
  createdAt: string;
  server: string;
  /** What the room does differently from the default gameplay: feature to preset. */
  gameplay?: Record<string, string> | null;
}

/** Requests carry the admin key, or the device token of the admin account; a failure has the HTTP status in `status`. */
export interface AdminHttp {
  get(url: string, adminKey: string): Promise<AdminRoom[]>;
  delete(url: string, adminKey: string): Promise<void>;
  put(url: string, adminKey: string, body: unknown): Promise<void>;
  post(url: string, adminKey: string): Promise<void>;
}

export interface KeyStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

export type AdminStatus = "signedOut" | "loading" | "ready" | "error";
export type AdminError = "invalidKey" | "unavailable" | "serverUnreachable" | "notFound";

/** What starting a world event in a room came to. `notNow`: it is off there, nobody is in the room, or it is already going. */
export type TriggerResult = "started" | "notNow" | AdminError;

export const ADMIN_KEY_STORAGE_KEY = "snake.adminKey";

/** The world events an administrator can start in a room right now, by the names the manager takes, in the order they are shown. */
export const WORLD_EVENT_TRIGGERS = ["lightning", "meteors", "turret", "zone", "fruitshower", "bounty", "mines", "sugarrush"] as const;

function withStatus(failure: unknown): Error & { status?: number } {
  const status = axios.isAxiosError(failure) ? failure.response?.status : undefined;
  return Object.assign(new Error(failure instanceof Error ? failure.message : "Request failed"), { status });
}

const axiosHttp: AdminHttp = {
  async get(url, adminKey) {
    try {
      return (await axios.get<AdminRoom[]>(url, { headers: adminHeaders(adminKey) })).data;
    } catch (failure) {
      throw withStatus(failure);
    }
  },
  async delete(url, adminKey) {
    try {
      await axios.delete(url, { headers: adminHeaders(adminKey) });
    } catch (failure) {
      throw withStatus(failure);
    }
  },
  async put(url, adminKey, body) {
    try {
      await axios.put(url, body, { headers: adminHeaders(adminKey) });
    } catch (failure) {
      throw withStatus(failure);
    }
  },
  async post(url, adminKey) {
    try {
      await axios.post(url, null, { headers: adminHeaders(adminKey) });
    } catch (failure) {
      throw withStatus(failure);
    }
  },
};

export interface UseAdminRoomsOptions {
  managerUrl?: string;
  http?: AdminHttp;
  /** Where the key is kept. Session storage, so closing the tab signs out. */
  storage?: KeyStorage | null;
}

function defaultStorage(): KeyStorage | null {
  try {
    return typeof sessionStorage === "undefined" ? null : sessionStorage;
  } catch {
    return null;
  }
}

/** The administrator's view of all rooms: sign in with an admin key, list rooms, delete old ones. */
export function useAdminRooms(options: UseAdminRoomsOptions = {}) {
  const managerUrl = (options.managerUrl ?? resolveManagerUrl()).replace(/\/+$/, "");
  const http = options.http ?? axiosHttp;
  const storage = options.storage === undefined ? defaultStorage() : options.storage;

  const status = shallowRef<AdminStatus>("signedOut");
  const error = shallowRef<AdminError | null>(null);
  const rooms = shallowRef<AdminRoom[]>([]);
  let key = readKey();

  function readKey(): string | null {
    try {
      return storage?.getItem(ADMIN_KEY_STORAGE_KEY) || null;
    } catch {
      return null;
    }
  }

  function writeKey(value: string | null) {
    key = value;
    try {
      if (value) storage?.setItem(ADMIN_KEY_STORAGE_KEY, value);
      else storage?.removeItem(ADMIN_KEY_STORAGE_KEY);
    } catch {
      // The key then only lives as long as this page.
    }
  }

  function rejectKey() {
    writeKey(null);
    rooms.value = [];
    status.value = "signedOut";
    error.value = "invalidKey";
  }

  async function load(candidate: string) {
    status.value = "loading";
    error.value = null;
    try {
      rooms.value = await http.get(`${managerUrl}/admin/rooms`, candidate);
      writeKey(candidate);
      status.value = "ready";
    } catch (failure) {
      if (isRejected((failure as { status?: number }).status)) return rejectKey();
      status.value = "error";
      error.value = "unavailable";
    }
  }

  async function signIn(adminKey: string) {
    await load(adminKey.trim());
  }

  /** Signs in as the admin account the player is signed in with. No key: the server looks at the role of the account. */
  async function signInWithAccount() {
    await load(ACCOUNT_CREDENTIAL);
  }

  async function refresh() {
    if (key) await load(key);
  }

  async function remove(id: string) {
    if (!key) return;
    try {
      await http.delete(`${managerUrl}/admin/rooms/${id}`, key);
    } catch (failure) {
      const code = (failure as { status?: number }).status;
      if (isRejected(code)) return rejectKey();
      error.value = code === 502 ? "serverUnreachable" : code === 404 ? "notFound" : "unavailable";
      return;
    }
    await refresh();
  }

  const errorOf = (code: number | undefined): AdminError => (code === 502 ? "serverUnreachable" : code === 404 ? "notFound" : "unavailable");

  /** Changes what a room does with the gameplay. Only what differs from the default is sent. True when the room took it. */
  async function setGameplay(id: string, choices: Record<string, string>): Promise<boolean> {
    if (!key) return false;
    const body = Object.fromEntries(Object.entries(choices).filter(([, preset]) => preset !== "inherit"));
    try {
      await http.put(`${managerUrl}/admin/rooms/${id}/gameplay`, key, body);
    } catch (failure) {
      const code = (failure as { status?: number }).status;
      if (isRejected(code)) rejectKey();
      else error.value = errorOf(code);
      return false;
    }
    await refresh();
    return true;
  }

  /** Starts a world event (one of `WORLD_EVENT_TRIGGERS`) in a room now. */
  async function trigger(id: string, kind: string): Promise<TriggerResult> {
    if (!key) return "invalidKey";
    try {
      await http.post(`${managerUrl}/admin/rooms/${id}/events/${kind}`, key);
    } catch (failure) {
      const code = (failure as { status?: number }).status;
      if (isRejected(code)) {
        rejectKey();
        return "invalidKey";
      }
      return code === 409 ? "notNow" : errorOf(code);
    }
    return "started";
  }

  function signOut() {
    writeKey(null);
    rooms.value = [];
    error.value = null;
    status.value = "signedOut";
  }

  return { status, error, rooms, signIn, signInWithAccount, refresh, remove, setGameplay, trigger, signOut, hasKey: () => key !== null };
}
