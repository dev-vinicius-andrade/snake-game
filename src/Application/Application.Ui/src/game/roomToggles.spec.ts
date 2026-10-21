import { describe, expect, it } from "vitest";
import type { GameplayConfig } from "@/composables/useAdminGameplay";
import { EVENT_TOGGLES, catalogFromConfig, catalogFromReply, itemsOnByDefault } from "./roomToggles";

const room = { id: "11111111-1111-1111-1111-111111111111", code: "ABC234", name: "Friends" };

/** A config with just what the catalog reads. */
function config(overrides: Partial<Record<"perks" | "prizes" | "rewards" | "events", unknown>> = {}): GameplayConfig {
  const events: Record<string, unknown> = {};
  for (const [key, value] of Object.entries({
    lightning: {},
    meteors: {},
    turret: {},
    zone: {},
    fruitShower: {},
    bounty: {},
    mines: {},
    sugarRush: {},
    maze: {},
    ...((overrides.events as object) ?? {}),
  })) {
    events[key] = { enabled: true, rooms: [], excludedRooms: [], ...(value as object) };
  }
  return {
    perks: {
      enabled: true,
      perks: (overrides.perks as unknown[]) ?? [
        { id: "magnet", name: "Magnet", enabled: true, rooms: [], excludedRooms: [], effects: [{ effect: "Magnet", amount: 1 }] },
        { id: "script", name: "Scripted", enabled: true, rooms: [], excludedRooms: [], effects: [], script: "function modify() {}" },
        { id: "old", name: "Old", enabled: false, rooms: [], excludedRooms: [], effects: [] },
      ],
    },
    prize: { enabled: true, prizes: (overrides.prizes as unknown[]) ?? [{ id: "gold", name: "Gold", enabled: true, rooms: [], excludedRooms: [] }] },
    rewards: (overrides.rewards as unknown[]) ?? [{ id: "life", name: "Extra life", reward: {} }],
    worldEvents: { enabled: true, ...events },
  } as unknown as GameplayConfig;
}

describe("EVENT_TOGGLES", () => {
  it("are the nine events by the names the server switches them under, the storms with an intensity", () => {
    expect(EVENT_TOGGLES.map((event) => event.id)).toEqual(["lightning", "meteors", "turret", "zone", "fruit_shower", "bounty", "mine_field", "sugar_rush", "maze"]);
    expect(EVENT_TOGGLES.filter((event) => event.intensity).map((event) => event.id)).toEqual(["lightning", "meteors", "turret", "zone"]);
  });
});

describe("catalogFromConfig", () => {
  it("lists what the administrator made, by id and name, each with whether it is on in a room that changes nothing", () => {
    const catalog = catalogFromConfig(config(), room);

    expect(catalog.perks.map((perk) => [perk.id, perk.name, perk.onByDefault])).toEqual([
      ["magnet", "Magnet", true],
      ["script", "Scripted", false],
      ["old", "Old", false],
    ]);
    expect(catalog.prizes.map((prize) => [prize.id, prize.onByDefault])).toEqual([["gold", true]]);
    expect(catalog.rewards.map((reward) => [reward.id, reward.name, reward.onByDefault])).toEqual([["life", "Extra life", true]]);
    expect(catalog.events).toHaveLength(9);
    expect(catalog.events.every((event) => event.onByDefault)).toBe(true);
  });

  it("follows the old room lists for the room, so what runs there is what the toggles start from", () => {
    const catalog = catalogFromConfig(
      config({
        perks: [{ id: "magnet", name: "Magnet", enabled: true, rooms: ["OTHER1"], excludedRooms: [], effects: [] }],
        prizes: [{ id: "gold", name: "Gold", enabled: true, rooms: [], excludedRooms: ["friends"] }],
        events: { maze: { rooms: ["ABC234"] }, zone: { excludedRooms: [room.id] }, bounty: { rooms: ["OTHER1"] } },
      }),
      room
    );

    expect(catalog.perks[0].onByDefault).toBe(false);
    expect(catalog.prizes[0].onByDefault).toBe(false);
    const events = Object.fromEntries(catalog.events.map((event) => [event.id, event.onByDefault]));
    expect(events).toMatchObject({ maze: true, zone: false, bounty: false, lightning: true });
  });

  it("marks what still carries an old room list, so the page can say so", () => {
    const catalog = catalogFromConfig(config({ events: { maze: { rooms: ["ABC234"] } } }), room);

    expect(catalog.events.find((event) => event.id === "maze")!.legacy).toBe(true);
    expect(catalog.events.find((event) => event.id === "zone")!.legacy).toBe(false);
  });

  it("with no room, a thing is on by default when it is enabled and, for a perk, has no script", () => {
    const catalog = catalogFromConfig(config(), null);

    expect(catalog.perks.map((perk) => perk.onByDefault)).toEqual([true, false, false]);
  });

  it("carries the master switches", () => {
    const catalog = catalogFromConfig(config(), room);

    expect([catalog.perksEnabled, catalog.prizesEnabled, catalog.worldEventsEnabled]).toEqual([true, true, true]);
  });
});

describe("catalogFromReply", () => {
  it("reads what the public list says", () => {
    const catalog = catalogFromReply({
      perksEnabled: true,
      prizesEnabled: false,
      worldEventsEnabled: true,
      perks: [{ id: "m", name: "Magnet", onByDefault: true }],
      prizes: [{ id: "g", name: "Gold", onByDefault: false }],
      rewards: [{ id: "r", name: "Life", onByDefault: true }],
      events: [{ id: "maze", onByDefault: false, intensity: false }, { id: "zone", onByDefault: true, intensity: true }],
    });

    expect(catalog.perks).toEqual([{ id: "m", name: "Magnet", onByDefault: true, legacy: false }]);
    expect(catalog.prizesEnabled).toBe(false);
    expect(catalog.events.map((event) => [event.id, event.onByDefault, event.intensity])).toEqual([["maze", false, false], ["zone", true, true]]);
  });

  it("is an empty catalog for something that is not a list, and skips entries it cannot use", () => {
    expect(catalogFromReply(null).perks).toEqual([]);
    expect(catalogFromReply("nope").events).toEqual([]);
    const catalog = catalogFromReply({ perks: [{ id: 7 }, null, { id: "ok" }], events: [{ id: "ghost" }, { id: "maze" }] });
    expect(catalog.perks.map((perk) => perk.id)).toEqual(["ok"]);
    expect(catalog.events.map((event) => event.id)).toEqual(["maze"]);
  });
});

describe("itemsOnByDefault", () => {
  it("counts the ones on", () => {
    expect(itemsOnByDefault([{ id: "a", name: "A", onByDefault: true, legacy: false }, { id: "b", name: "B", onByDefault: false, legacy: false }])).toBe(1);
  });
});
