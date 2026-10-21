import { describe, expect, it, vi } from "vitest";
import { useRoomToggleCatalog } from "./useRoomToggleCatalog";

describe("useRoomToggleCatalog", () => {
  it("starts empty", () => {
    const { catalog, failed } = useRoomToggleCatalog({ managerUrl: "http://manager.test", http: { get: vi.fn() } });

    expect(catalog.value.perks).toEqual([]);
    expect(failed.value).toBe(false);
  });

  it("asks the manager for the public list and keeps what it says", async () => {
    const get = vi.fn(async () => ({ perks: [{ id: "m", name: "Magnet", onByDefault: true }], events: [{ id: "maze", onByDefault: false, intensity: false }] }));
    const { catalog, load } = useRoomToggleCatalog({ managerUrl: "http://manager.test/", http: { get } });

    await load();

    expect(get).toHaveBeenCalledWith("http://manager.test/gameplay/toggles");
    expect(catalog.value.perks.map((perk) => perk.id)).toEqual(["m"]);
    expect(catalog.value.events.map((event) => event.id)).toEqual(["maze"]);
  });

  it("says so when the list could not be had, and the room is made with the default gameplay", async () => {
    const { catalog, failed, load } = useRoomToggleCatalog({ managerUrl: "http://manager.test", http: { get: vi.fn(async () => Promise.reject(new Error("down"))) } });

    await load();

    expect(failed.value).toBe(true);
    expect(catalog.value.perks).toEqual([]);
  });
});
