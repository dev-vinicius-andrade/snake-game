import { describe, expect, it } from "vitest";
import type { PerkDefinition } from "@/composables/useAdminGameplay";
import { perkWorksInRoom, roomKey, setPerkInRoom, type RoomRef } from "./perkRooms";

const room: RoomRef = { id: "11111111-1111-1111-1111-111111111111", code: "ABC234", name: "Friends" };
const publicRoom: RoomRef = { id: "22222222-2222-2222-2222-222222222222", code: null, name: null };

const perk = (overrides: Partial<PerkDefinition> = {}): PerkDefinition => ({
  id: "p",
  name: "P",
  foodsRequired: 5,
  effects: [{ effect: "Magnet", amount: 10 }],
  rooms: [],
  excludedRooms: [],
  ...overrides,
});
const scripted = (overrides: Partial<PerkDefinition> = {}) => perk({ effects: [], script: "function modify() { return {}; }", ...overrides });

describe("roomKey", () => {
  it("is the invite code when there is one, else the id", () => {
    expect(roomKey(room)).toBe("ABC234");
    expect(roomKey(publicRoom)).toBe("22222222-2222-2222-2222-222222222222");
  });
});

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

  it("is blind to a blank name in a room that has none", () => {
    expect(perkWorksInRoom(perk({ rooms: [""] }), publicRoom)).toBe(false);
  });
});

describe("setPerkInRoom", () => {
  it("switches an ordinary perk off in a room by leaving the room out", () => {
    const changed = setPerkInRoom(perk(), room, false);

    expect(changed.excludedRooms).toEqual(["ABC234"]);
    expect(changed.rooms).toEqual([]);
    expect(perkWorksInRoom(changed, room)).toBe(false);
    expect(perkWorksInRoom(changed, publicRoom)).toBe(true);
  });

  it("switches it on again by taking the room out of the left-out list", () => {
    const off = setPerkInRoom(perk(), room, false);

    const on = setPerkInRoom(off, room, true);

    expect(on.excludedRooms).toEqual([]);
    expect(on.rooms).toEqual([]);
    expect(perkWorksInRoom(on, room)).toBe(true);
  });

  it("an ordinary perk meant for some rooms is switched on in another room by naming it", () => {
    const changed = setPerkInRoom(perk({ rooms: ["OTHER1"] }), room, true);

    expect(changed.rooms).toEqual(["OTHER1", "ABC234"]);
    expect(perkWorksInRoom(changed, room)).toBe(true);
  });

  it("an ordinary perk meant for some rooms is switched off in one of them without becoming a perk for every room", () => {
    const changed = setPerkInRoom(perk({ rooms: ["ABC234", "OTHER1"] }), room, false);

    expect(perkWorksInRoom(changed, room)).toBe(false);
    expect(perkWorksInRoom(changed, publicRoom)).toBe(false);
    expect(changed.rooms).toContain("OTHER1");
  });

  it("a perk with a script is switched on by naming the room", () => {
    const changed = setPerkInRoom(scripted(), room, true);

    expect(changed.rooms).toEqual(["ABC234"]);
    expect(changed.excludedRooms).toEqual([]);
    expect(perkWorksInRoom(changed, room)).toBe(true);
  });

  it("and off by taking the room away again", () => {
    const changed = setPerkInRoom(scripted({ rooms: ["ABC234", "OTHER1"] }), room, false);

    expect(changed.rooms).toEqual(["OTHER1"]);
    expect(perkWorksInRoom(changed, room)).toBe(false);
  });

  it("finds a room whatever way it was named, and does not name it twice", () => {
    const byName = scripted({ rooms: ["friends"] });

    const again = setPerkInRoom(byName, room, true);
    const off = setPerkInRoom(byName, room, false);

    expect(again.rooms).toEqual(["friends"]);
    expect(off.rooms).toEqual([]);
  });

  it("a room without a code is named by its id", () => {
    const changed = setPerkInRoom(scripted(), publicRoom, true);

    expect(changed.rooms).toEqual([publicRoom.id]);
    expect(perkWorksInRoom(changed, publicRoom)).toBe(true);
  });

  it("does not touch the perk it was given", () => {
    const original = perk();

    setPerkInRoom(original, room, false);

    expect(original.excludedRooms).toEqual([]);
  });

  it("leaves the rest of the perk as it was", () => {
    const changed = setPerkInRoom(scripted({ name: "Keep me" }), room, true);

    expect(changed.name).toBe("Keep me");
    expect(changed.script).toContain("modify");
  });
});
