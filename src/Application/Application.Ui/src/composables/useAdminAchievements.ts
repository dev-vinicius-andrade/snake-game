import axios from "axios";
import { shallowRef } from "vue";
import { resolveManagerUrl } from "@/config/env";
import { adminHeaders, isRejected } from "./adminAuth";
import { ADMIN_KEY_STORAGE_KEY, type KeyStorage } from "./useAdminRooms";

export interface AchievementText {
  name: string;
  description: string;
}

/** An achievement as the admin api has it. `texts` has only the languages an administrator wrote: the built-in ones have none. */
export interface AdminAchievement {
  id: string;
  /** A game icon (`game-icons:snake`) or a Material Design icon (`mdi-trophy`). */
  icon: string;
  stat: string;
  threshold: number;
  enabled: boolean;
  texts: Record<string, AchievementText>;
}

export interface AchievementsPayload {
  achievements: AdminAchievement[];
  /** The stats that can unlock an achievement. */
  stats: string[];
  /** False when the manager has no database: the achievements are the code's and cannot be changed. */
  writable: boolean;
}

/** A failure has the HTTP status in `status`, and what the server said in `serverMessage`. */
export interface AchievementsHttp {
  get(url: string, adminKey: string): Promise<AchievementsPayload>;
  put(url: string, adminKey: string, body: unknown): Promise<void>;
  delete(url: string, adminKey: string): Promise<void>;
}

export type AchievementsStatus = "idle" | "loading" | "ready" | "signedOut" | "error";

function withStatus(failure: unknown): Error & { status?: number; serverMessage?: string } {
  const response = axios.isAxiosError(failure) ? failure.response : undefined;
  const serverMessage = typeof response?.data?.error === "string" ? response.data.error : undefined;
  return Object.assign(new Error(failure instanceof Error ? failure.message : "Request failed"), { status: response?.status, serverMessage });
}

const axiosHttp: AchievementsHttp = {
  async get(url, adminKey) {
    try {
      return (await axios.get<AchievementsPayload>(url, { headers: adminHeaders(adminKey) })).data;
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
  async delete(url, adminKey) {
    try {
      await axios.delete(url, { headers: adminHeaders(adminKey) });
    } catch (failure) {
      throw withStatus(failure);
    }
  },
};

export interface UseAdminAchievementsOptions {
  managerUrl?: string;
  http?: AchievementsHttp;
  /** Where the admin key is kept; the one the admin page signed in with. */
  storage?: KeyStorage | null;
}

function defaultStorage(): KeyStorage | null {
  try {
    return typeof sessionStorage === "undefined" ? null : sessionStorage;
  } catch {
    return null;
  }
}

function isPayload(data: unknown): data is AchievementsPayload {
  const body = data as Partial<AchievementsPayload> | null;
  return !!body && Array.isArray(body.achievements) && Array.isArray(body.stats) && typeof body.writable === "boolean";
}

/** The administrator's editor for the achievements of the game: the list, adding and changing one, switching one off, removing one. */
export function useAdminAchievements(options: UseAdminAchievementsOptions = {}) {
  const base = `${(options.managerUrl ?? resolveManagerUrl()).replace(/\/+$/, "")}/admin/achievements`;
  const http = options.http ?? axiosHttp;
  const storage = options.storage === undefined ? defaultStorage() : options.storage;

  const status = shallowRef<AchievementsStatus>("idle");
  /** `unavailable`, `invalidKey`, or a code of the server: `invalidId`, `invalidIcon`, `invalidStat`, `invalidThreshold`, `invalidText`, `readOnly`, `inUse`, `notFound`. */
  const error = shallowRef<string | null>(null);
  const entries = shallowRef<AdminAchievement[]>([]);
  const stats = shallowRef<string[]>([]);
  const writable = shallowRef(true);

  function key(): string | null {
    try {
      return storage?.getItem(ADMIN_KEY_STORAGE_KEY) || null;
    } catch {
      return null;
    }
  }

  /** Notes why a call failed. True when the credential was refused, which signs the editor out. */
  function failWith(failure: unknown): boolean {
    const { status: code, serverMessage } = failure as { status?: number; serverMessage?: string };
    if (isRejected(code)) {
      status.value = "signedOut";
      error.value = "invalidKey";
      return true;
    }
    error.value = serverMessage ?? "unavailable";
    return false;
  }

  async function load(): Promise<void> {
    const credential = key();
    if (!credential) return;
    status.value = "loading";
    error.value = null;
    try {
      const data = await http.get(base, credential);
      if (!isPayload(data)) throw new Error("Unexpected answer");
      entries.value = data.achievements;
      stats.value = data.stats;
      writable.value = data.writable;
      status.value = "ready";
    } catch (failure) {
      if (!failWith(failure)) status.value = "error";
    }
  }

  async function save(entry: AdminAchievement): Promise<boolean> {
    const credential = key();
    if (!credential) return false;
    error.value = null;
    const texts = Object.fromEntries(
      Object.entries(entry.texts)
        .filter(([, text]) => text.name.trim() !== "")
        .map(([locale, text]) => [locale, { name: text.name.trim(), description: text.description.trim() }])
    );
    try {
      await http.put(`${base}/${encodeURIComponent(entry.id)}`, credential, { icon: entry.icon, stat: entry.stat, threshold: entry.threshold, enabled: entry.enabled, texts });
    } catch (failure) {
      failWith(failure);
      return false;
    }
    await load();
    return true;
  }

  async function remove(id: string): Promise<boolean> {
    const credential = key();
    if (!credential) return false;
    error.value = null;
    try {
      await http.delete(`${base}/${encodeURIComponent(id)}`, credential);
    } catch (failure) {
      failWith(failure);
      return false;
    }
    await load();
    return true;
  }

  return { status, error, entries, stats, writable, load, save, remove };
}
