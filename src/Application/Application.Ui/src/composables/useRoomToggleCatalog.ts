import axios from "axios";
import { shallowRef } from "vue";
import { resolveManagerUrl } from "@/config/env";
import { catalogFromReply, emptyCatalog, type RoomToggleCatalog } from "@/game/roomToggles";

export interface RoomToggleCatalogHttp {
  get(url: string): Promise<unknown>;
}

const axiosHttp: RoomToggleCatalogHttp = { get: async (url) => (await axios.get<unknown>(url)).data };

export interface UseRoomToggleCatalogOptions {
  managerUrl?: string;
  http?: RoomToggleCatalogHttp;
}

/**
 * What a room can switch on or off: the perks, prizes, rewards and world events the administrator made. A person who makes a private
 * room chooses among these and creates nothing; the manager hands the list to anyone, with no scripts and no room names.
 */
export function useRoomToggleCatalog(options: UseRoomToggleCatalogOptions = {}) {
  const managerUrl = (options.managerUrl ?? resolveManagerUrl()).replace(/\/+$/, "");
  const http = options.http ?? axiosHttp;
  const catalog = shallowRef<RoomToggleCatalog>(emptyCatalog());
  const failed = shallowRef(false);

  async function load() {
    failed.value = false;
    try {
      catalog.value = catalogFromReply(await http.get(`${managerUrl}/gameplay/toggles`));
    } catch {
      // Without the list the room is made with the default gameplay.
      failed.value = true;
    }
  }

  return { catalog, failed, load };
}
