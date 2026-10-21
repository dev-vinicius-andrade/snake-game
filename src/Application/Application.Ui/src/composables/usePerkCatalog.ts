import axios from "axios";
import { shallowRef } from "vue";
import { resolveManagerUrl } from "@/config/env";

export interface CatalogEffect {
  effect: string;
  amount: number;
  /** For event resistance: the one event it is for. Left out: all of them. */
  target?: string | null;
}

/** A step up of a perk: with this many foods everything the perk does is `multiplier` times its amounts. */
export interface CatalogTier {
  foodsRequired: number;
  multiplier: number;
  /** A tier eating never reaches: only a reward lifts a perk into it. */
  rewardOnly?: boolean;
}

export interface CatalogPerk {
  id: string;
  name: string;
  foodsRequired: number;
  effects: CatalogEffect[];
  /** The steps up after the first tier. Empty for a perk that has none. */
  tiers: CatalogTier[];
  /** A Material Design Icons name; without one the icon of the first effect is used. */
  icon?: string | null;
  /** A sentence that says what the perk does, for a perk whose effects are in a script. */
  description?: string | null;
  /** Seconds a cooldown ability of the perk takes to recharge; left out or 0 for a perk that has none. */
  cooldownSeconds?: number;
  /** Seconds a shield burst, dash or vacuum burst lasts. */
  durationSeconds?: number;
}

/** What the manager sends: a perk from before tiers has none. */
export type ReplyPerk = Omit<CatalogPerk, "tiers"> & { tiers?: CatalogTier[] };

export interface PerkCatalogReply {
  revision: string;
  perks: ReplyPerk[];
}

export interface PerkCatalogHttp {
  get(url: string): Promise<PerkCatalogReply>;
}

const axiosHttp: PerkCatalogHttp = { get: async (url) => (await axios.get<PerkCatalogReply>(url)).data };

export interface UsePerkCatalogOptions {
  managerUrl?: string;
  http?: PerkCatalogHttp;
}

const isTier = (value: unknown): value is CatalogTier =>
  typeof value === "object" &&
  value !== null &&
  Number.isFinite((value as CatalogTier).foodsRequired) &&
  Number.isFinite((value as CatalogTier).multiplier);

const isPerk = (value: unknown): value is ReplyPerk =>
  typeof value === "object" &&
  value !== null &&
  typeof (value as ReplyPerk).id === "string" &&
  Array.isArray((value as ReplyPerk).effects) &&
  (value as ReplyPerk).effects.every((entry) => typeof entry?.effect === "string" && Number.isFinite(entry.amount));

/** A perk from the server with its tiers made safe: a list, and only the entries that are a pair of numbers. */
const withTiers = (perk: ReplyPerk): CatalogPerk => ({ ...perk, tiers: Array.isArray(perk.tiers) ? perk.tiers.filter(isTier) : [] });

/** What each perk is and does, so a perk id from a snapshot can be shown as an icon with a tooltip. */
export function usePerkCatalog(options: UsePerkCatalogOptions = {}) {
  const managerUrl = (options.managerUrl ?? resolveManagerUrl()).replace(/\/+$/, "");
  const http = options.http ?? axiosHttp;
  const perks = shallowRef<CatalogPerk[]>([]);

  async function load() {
    try {
      const reply = await http.get(`${managerUrl}/gameplay/perks`);
      if (Array.isArray(reply?.perks)) perks.value = reply.perks.filter(isPerk).map(withTiers);
    } catch {
      // Without the catalog a perk is still shown, just with its id.
    }
  }

  const find = (id: string) => perks.value.find((perk) => perk.id === id);

  return { perks, load, find };
}
