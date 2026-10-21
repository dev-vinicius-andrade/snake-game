import type { PerkDefinition } from "@/composables/useAdminGameplay";

/** A room as the admin page knows it. A room can be named by its invite code, its name or its id. */
export interface RoomRef {
  id: string;
  code: string | null;
  name: string | null;
}

const same = (first: string, second: string | null) => second !== null && first.trim().length > 0 && first.trim().toLowerCase() === second.toLowerCase();

export const isNamed = (entry: string, place: RoomRef) => same(entry, place.code) || same(entry, place.name) || same(entry, place.id);

/**
 * The old room lists of a perk, a prize or an event, as the game server still reads them for a room that made no choice of its own: a room
 * in the left-out list is out; with a list of rooms only those are in; with neither list, every room is in, unless `offUntilNamed`.
 */
export function listsAllow(item: { rooms: string[]; excludedRooms: string[] }, place: RoomRef, offUntilNamed: boolean): boolean {
  if (item.excludedRooms.some((entry) => isNamed(entry, place))) return false;
  if (item.rooms.length === 0) return !offUntilNamed;
  return item.rooms.some((entry) => isNamed(entry, place));
}

const hasScript = (perk: PerkDefinition) => !!perk.script?.trim();

/** The same rule the game server follows for a room that made no choice: does this perk work in this room? */
export function perkWorksInRoom(perk: PerkDefinition, place: RoomRef): boolean {
  return perk.enabled && listsAllow(perk, place, hasScript(perk));
}
