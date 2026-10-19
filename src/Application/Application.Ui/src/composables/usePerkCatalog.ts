import axios from "axios";
import { shallowRef } from "vue";
import { resolveManagerUrl } from "@/config/env";

export interface CatalogEffect {
  effect: string;
  amount: number;
}

export interface CatalogPerk {
  id: string;
  name: string;
  foodsRequired: number;
  effects: CatalogEffect[];
}

export interface PerkCatalogReply {
  revision: string;
  perks: CatalogPerk[];
}

export interface PerkCatalogHttp {
  get(url: string): Promise<PerkCatalogReply>;
}

const axiosHttp: PerkCatalogHttp = { get: async (url) => (await axios.get<PerkCatalogReply>(url)).data };

export interface UsePerkCatalogOptions {
  managerUrl?: string;
  http?: PerkCatalogHttp;
}

const isPerk = (value: unknown): value is CatalogPerk =>
  typeof value === "object" &&
  value !== null &&
  typeof (value as CatalogPerk).id === "string" &&
  Array.isArray((value as CatalogPerk).effects) &&
  (value as CatalogPerk).effects.every((entry) => typeof entry?.effect === "string" && Number.isFinite(entry.amount));

/** What each perk is and does, so a perk id from a snapshot can be shown as an icon with a tooltip. */
export function usePerkCatalog(options: UsePerkCatalogOptions = {}) {
  const managerUrl = (options.managerUrl ?? resolveManagerUrl()).replace(/\/+$/, "");
  const http = options.http ?? axiosHttp;
  const perks = shallowRef<CatalogPerk[]>([]);

  async function load() {
    try {
      const reply = await http.get(`${managerUrl}/gameplay/perks`);
      if (Array.isArray(reply?.perks)) perks.value = reply.perks.filter(isPerk);
    } catch {
      // Without the catalog a perk is still shown, just with its id.
    }
  }

  const find = (id: string) => perks.value.find((perk) => perk.id === id);

  return { perks, load, find };
}
