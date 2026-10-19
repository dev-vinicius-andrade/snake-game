import type { PerkDefinition } from "@/composables/useAdminGameplay";

/** A room as the admin page knows it. A room can be named by its invite code, its name or its id. */
export interface RoomRef {
  id: string;
  code: string | null;
  name: string | null;
}

/** The name a room is given when an administrator picks it: the invite code, which people know, else the id. */
export const roomKey = (room: RoomRef): string => room.code ?? room.id;

const same = (first: string, second: string | null) => second !== null && first.trim().length > 0 && first.trim().toLowerCase() === second.toLowerCase();

const isNamed = (entry: string, room: RoomRef) => same(entry, room.code) || same(entry, room.name) || same(entry, room.id);

const hasScript = (perk: PerkDefinition) => !!perk.script?.trim();

/** The same rule the game server follows: does this perk work in this room? */
export function perkWorksInRoom(perk: PerkDefinition, room: RoomRef): boolean {
  if (perk.excludedRooms.some((entry) => isNamed(entry, room))) return false;
  if (perk.rooms.length === 0) return !hasScript(perk);
  return perk.rooms.some((entry) => isNamed(entry, room));
}

/**
 * The perk with it switched on or off in one room, whatever way the room was named before. A perk with a script is on where it
 * names rooms. Any other perk is on everywhere unless it names rooms, and is switched off in a room by leaving that room out.
 * The perk it is given is not changed.
 */
export function setPerkInRoom(perk: PerkDefinition, room: RoomRef, enabled: boolean): PerkDefinition {
  const withoutRoom = (entries: string[]) => entries.filter((entry) => !isNamed(entry, room));
  const named = (entries: string[]) => entries.some((entry) => isNamed(entry, room));
  const key = roomKey(room);

  if (hasScript(perk)) {
    return { ...perk, rooms: enabled && !named(perk.rooms) ? [...perk.rooms, key] : enabled ? perk.rooms : withoutRoom(perk.rooms), excludedRooms: withoutRoom(perk.excludedRooms) };
  }
  if (enabled) {
    const rooms = perk.rooms.length > 0 && !named(perk.rooms) ? [...perk.rooms, key] : perk.rooms;
    return { ...perk, rooms, excludedRooms: withoutRoom(perk.excludedRooms) };
  }
  return { ...perk, excludedRooms: named(perk.excludedRooms) ? perk.excludedRooms : [...perk.excludedRooms, key] };
}
