import { shallowRef } from "vue";
import { resolveManagerUrl } from "@/config/env";
import type { AchievementDefinition, AchievementUnlock } from "@/game/achievements";
import {
  axiosHttp,
  clearStoredAccountToken,
  readStoredAccountToken,
  tokenRejected,
  type AccountHttp,
  type AccountStorage,
} from "./useAccount";

export type ProfileStatus = "idle" | "loading" | "ready" | "signedOut" | "error";

export interface ProfileAccount {
  id: string;
  username: string;
  tag: number;
  display: string;
  createdAt: string;
}

export interface Profile {
  account: ProfileAccount;
  stats: Record<string, number>;
  achievements: AchievementUnlock[];
}

export interface UseProfileOptions {
  managerUrl?: string;
  http?: AccountHttp;
  storage?: AccountStorage;
}

/** The achievement list is the same for everybody and rarely changes: kept for the life of the page, per manager. */
const definitionsCache = new Map<string, Promise<AchievementDefinition[]>>();

export function clearAchievementDefinitionsCache() {
  definitionsCache.clear();
}

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null ? (value as Record<string, unknown>) : {};
}

export function parseDefinitions(data: unknown): AchievementDefinition[] {
  if (!Array.isArray(data)) throw new Error("Invalid achievements answer");
  return data.flatMap((raw) => {
    const { id, icon, stat, threshold } = asRecord(raw);
    if (typeof id !== "string" || typeof stat !== "string" || typeof threshold !== "number") return [];
    return [{ id, icon: typeof icon === "string" ? icon : "", stat, threshold }];
  });
}

export function parseProfile(data: unknown): Profile {
  const body = asRecord(data);
  const account = asRecord(body.account);
  const { id, username, tag, display, createdAt } = account;
  if (typeof id !== "string" || typeof username !== "string" || typeof tag !== "number" || typeof display !== "string") {
    throw new Error("Invalid profile answer");
  }
  const stats: Record<string, number> = {};
  Object.entries(asRecord(body.stats)).forEach(([key, value]) => {
    if (typeof value === "number" && Number.isFinite(value)) stats[key] = value;
  });
  const achievements = (Array.isArray(body.achievements) ? body.achievements : []).flatMap((raw) => {
    const unlock = asRecord(raw);
    return typeof unlock.id === "string" ? [{ id: unlock.id, unlockedAt: typeof unlock.unlockedAt === "string" ? unlock.unlockedAt : "" }] : [];
  });
  return { account: { id, username, tag, display, createdAt: typeof createdAt === "string" ? createdAt : "" }, stats, achievements };
}

/** The signed-in player's stats and unlocked achievements, and the list of every achievement. A 401 means signed out. */
export function useProfile(options: UseProfileOptions = {}) {
  const http = options.http ?? axiosHttp;
  const base = (options.managerUrl ?? resolveManagerUrl()).replace(/\/+$/, "");

  const status = shallowRef<ProfileStatus>("idle");
  const profile = shallowRef<Profile | null>(null);
  const definitions = shallowRef<AchievementDefinition[]>([]);

  async function loadDefinitions(): Promise<AchievementDefinition[]> {
    let pending = definitionsCache.get(base);
    if (!pending) {
      pending = http.request({ method: "GET", url: `${base}/achievements` }).then((reply) => {
        if (reply.status !== 200) throw new Error("Could not load achievements");
        return parseDefinitions(reply.data);
      });
      definitionsCache.set(base, pending);
      pending.catch(() => definitionsCache.delete(base)); // A failure is not kept: the next call tries again.
    }
    definitions.value = await pending;
    return definitions.value;
  }

  async function loadProfile(): Promise<void> {
    const token = readStoredAccountToken(options.storage);
    if (!token) {
      profile.value = null;
      status.value = "signedOut";
      return;
    }
    const reply = await http.request({ method: "GET", url: `${base}/accounts/me/profile`, token });
    if (reply.status === 401) {
      clearStoredAccountToken(options.storage);
      tokenRejected.value = true;
      profile.value = null;
      status.value = "signedOut";
      return;
    }
    if (reply.status !== 200) throw new Error("Could not load the profile");
    profile.value = parseProfile(reply.data);
    status.value = "ready";
  }

  /** Loads the profile and the definitions together. Never rejects: `status` tells what happened. */
  async function load(): Promise<void> {
    status.value = "loading";
    try {
      await Promise.all([loadProfile(), loadDefinitions()]);
    } catch {
      status.value = "error";
    }
  }

  return { status, profile, definitions, load, loadDefinitions };
}
