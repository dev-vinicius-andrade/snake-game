import type { PerkDefinition } from "@/composables/useAdminGameplay";

/** A room as the admin page knows it. A room can be named by its invite code, its name or its id. */
export interface RoomRef {
  id: string;
  code: string | null;
  name: string | null;
}

/** The name a room is given when an administrator picks it: the invite code, which people know, else the id. */
export const roomKey = (place: RoomRef): string => place.code ?? place.id;

const same = (first: string, second: string | null) => second !== null && first.trim().length > 0 && first.trim().toLowerCase() === second.toLowerCase();

const isNamed = (entry: string, place: RoomRef) => same(entry, place.code) || same(entry, place.name) || same(entry, place.id);

const hasScript = (perk: PerkDefinition) => !!perk.script?.trim();

/** The same rule the game server follows: does this perk work in this room? */
export function perkWorksInRoom(perk: PerkDefinition, place: RoomRef): boolean {
  if (perk.excludedRooms.some((entry) => isNamed(entry, place))) return false;
  if (perk.rooms.length === 0) return !hasScript(perk);
  return perk.rooms.some((entry) => isNamed(entry, place));
}

/**
 * The perk with it switched on or off in one room, whatever way the room was named before. A perk with a script is on where it
 * names rooms. Any other perk is on everywhere unless it names rooms, and is switched off in a room by leaving that room out.
 * The perk it is given is not changed.
 */
export function setPerkInRoom(perk: PerkDefinition, place: RoomRef, enabled: boolean): PerkDefinition {
  const withoutRoom = (entries: string[]) => entries.filter((entry) => !isNamed(entry, place));
  const named = (entries: string[]) => entries.some((entry) => isNamed(entry, place));
  const key = roomKey(place);

  if (hasScript(perk)) {
    return { ...perk, rooms: enabled && !named(perk.rooms) ? [...perk.rooms, key] : enabled ? perk.rooms : withoutRoom(perk.rooms), excludedRooms: withoutRoom(perk.excludedRooms) };
  }
  if (enabled) {
    const rooms = perk.rooms.length > 0 && !named(perk.rooms) ? [...perk.rooms, key] : perk.rooms;
    return { ...perk, rooms, excludedRooms: withoutRoom(perk.excludedRooms) };
  }
  return { ...perk, excludedRooms: named(perk.excludedRooms) ? perk.excludedRooms : [...perk.excludedRooms, key] };
}
