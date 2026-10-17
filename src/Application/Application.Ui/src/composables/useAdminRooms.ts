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
}

/** Requests carry the admin key; a failure has the HTTP status in `status`. */
export interface AdminHttp {
  get(url: string, adminKey: string): Promise<AdminRoom[]>;
  delete(url: string, adminKey: string): Promise<void>;
}

export interface KeyStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

export type AdminStatus = "signedOut" | "loading" | "ready" | "error";
export type AdminError = "invalidKey" | "unavailable" | "serverUnreachable" | "notFound";

export const ADMIN_KEY_STORAGE_KEY = "snake.adminKey";
const ADMIN_KEY_HEADER = "X-Admin-Key";

function withStatus(failure: unknown): Error & { status?: number } {
  const status = axios.isAxiosError(failure) ? failure.response?.status : undefined;
  return Object.assign(new Error(failure instanceof Error ? failure.message : "Request failed"), { status });
}

const axiosHttp: AdminHttp = {
  async get(url, adminKey) {
    try {
      return (await axios.get<AdminRoom[]>(url, { headers: { [ADMIN_KEY_HEADER]: adminKey } })).data;
    } catch (failure) {
      throw withStatus(failure);
    }
  },
  async delete(url, adminKey) {
    try {
      await axios.delete(url, { headers: { [ADMIN_KEY_HEADER]: adminKey } });
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
      if ((failure as { status?: number }).status === 401) return rejectKey();
      status.value = "error";
      error.value = "unavailable";
    }
  }

  async function signIn(adminKey: string) {
    await load(adminKey.trim());
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
      if (code === 401) return rejectKey();
      error.value = code === 502 ? "serverUnreachable" : code === 404 ? "notFound" : "unavailable";
      return;
    }
    await refresh();
  }

  function signOut() {
    writeKey(null);
    rooms.value = [];
    error.value = null;
    status.value = "signedOut";
  }

  return { status, error, rooms, signIn, refresh, remove, signOut, hasKey: () => key !== null };
}
