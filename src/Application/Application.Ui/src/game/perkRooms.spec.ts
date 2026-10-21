import { describe, expect, it } from "vitest";
import type { PerkDefinition } from "@/composables/useAdminGameplay";
import { isNamed, listsAllow, perkWorksInRoom, type RoomRef } from "./perkRooms";

const room: RoomRef = { id: "11111111-1111-1111-1111-111111111111", code: "ABC234", name: "Friends" };
const publicRoom: RoomRef = { id: "22222222-2222-2222-2222-222222222222", code: null, name: null };

const perk = (overrides: Partial<PerkDefinition> = {}): PerkDefinition => ({
  id: "p",
  name: "P",
  foodsRequired: 5,
  enabled: true,
  effects: [{ effect: "Magnet", amount: 10 }],
  rooms: [],
  excludedRooms: [],
  ...overrides,
});
const scripted = (overrides: Partial<PerkDefinition> = {}) => perk({ effects: [], script: "function modify() { return {}; }", ...overrides });

describe("perkWorksInRoom", () => {
  it("an ordinary perk works everywhere", () => {
    expect(perkWorksInRoom(perk(), room)).toBe(true);
  });

  it("a perk with a script works nowhere until rooms are named", () => {
    expect(perkWorksInRoom(scripted(), room)).toBe(false);
  });

  it("a perk named for rooms works only in them, by code, name or id, in any case", () => {
    expect(perkWorksInRoom(perk({ rooms: ["abc234"] }), room)).toBe(true);
    expect(perkWorksInRoom(perk({ rooms: ["FRIENDS"] }), room)).toBe(true);
    expect(perkWorksInRoom(perk({ rooms: [room.id] }), room)).toBe(true);
    expect(perkWorksInRoom(perk({ rooms: ["OTHER1"] }), room)).toBe(false);
    expect(perkWorksInRoom(perk({ rooms: ["ABC234"] }), publicRoom)).toBe(false);
  });

  it("a left-out room wins over everything else", () => {
    expect(perkWorksInRoom(perk({ excludedRooms: ["ABC234"] }), room)).toBe(false);
    expect(perkWorksInRoom(perk({ rooms: ["ABC234"], excludedRooms: ["ABC234"] }), room)).toBe(false);
    expect(perkWorksInRoom(perk({ excludedRooms: ["ABC234"] }), publicRoom)).toBe(true);
  });

  it("a perk that is switched off works nowhere", () => {
    expect(perkWorksInRoom(perk({ enabled: false }), room)).toBe(false);
    expect(perkWorksInRoom(scripted({ enabled: false, rooms: ["ABC234"] }), room)).toBe(false);
  });

  it("is blind to a blank name in a room that has none", () => {
    expect(perkWorksInRoom(perk({ rooms: [""] }), publicRoom)).toBe(false);
  });
});

describe("listsAllow", () => {
  const item = (rooms: string[] = [], excludedRooms: string[] = []) => ({ rooms, excludedRooms });

  it("lets every room in when there is no list, unless it is off until named", () => {
    expect(listsAllow(item(), room, false)).toBe(true);
    expect(listsAllow(item(), room, true)).toBe(false);
  });

  it("lets in only the rooms of the list, and never a left-out one", () => {
    expect(listsAllow(item(["ABC234"]), room, false)).toBe(true);
    expect(listsAllow(item(["OTHER1"]), room, false)).toBe(false);
    expect(listsAllow(item(["ABC234"], ["friends"]), room, false)).toBe(false);
  });

  it("names a room by code, name or id in any case", () => {
    for (const entry of ["abc234", "FRIENDS", room.id]) expect(isNamed(entry, room)).toBe(true);
    expect(isNamed("  ", publicRoom)).toBe(false);
  });
});
