import type { GameplayConfig, HasRoomLists } from "@/composables/useAdminGameplay";
import { listsAllow, perkWorksInRoom, type RoomRef } from "./perkRooms";

/** One thing the administrator made that a room can switch: a perk, a prize, a reward or a world event. */
export interface ToggleItem {
  id: string;
  name: string;
  /** Whether it is on in a room that changes nothing. */
  onByDefault: boolean;
  /** It still carries an old room list, which decides for a room that made no choice. */
  legacy: boolean;
}

export interface EventToggleItem extends ToggleItem {
  /** The storms and the boss can also be made calm or wild. */
  intensity: boolean;
}

/** What a room can switch, with the master switches the administrator has on the Gameplay tab. */
export interface RoomToggleCatalog {
  perksEnabled: boolean;
  prizesEnabled: boolean;
  worldEventsEnabled: boolean;
  perks: ToggleItem[];
  prizes: ToggleItem[];
  rewards: ToggleItem[];
  events: EventToggleItem[];
}

/**
 * The world events a room can switch, by the names the server switches them under, in the order they are shown. `key` is where the
 * event is in the gameplay config; `trigger` is the name the manager takes to start it in a room.
 */
export const EVENT_TOGGLES = [
  { id: "lightning", key: "lightning", trigger: "lightning", intensity: true },
  { id: "meteors", key: "meteors", trigger: "meteors", intensity: true },
  { id: "turret", key: "turret", trigger: "turret", intensity: true },
  { id: "zone", key: "zone", trigger: "zone", intensity: true },
  { id: "fruit_shower", key: "fruitShower", trigger: "fruitshower", intensity: false },
  { id: "bounty", key: "bounty", trigger: "bounty", intensity: false },
  { id: "mine_field", key: "mines", trigger: "mines", intensity: false },
  { id: "sugar_rush", key: "sugarRush", trigger: "sugarrush", intensity: false },
  { id: "maze", key: "maze", trigger: "maze", intensity: false },
] as const;

export const emptyCatalog = (): RoomToggleCatalog => ({
  perksEnabled: true,
  prizesEnabled: true,
  worldEventsEnabled: true,
  perks: [],
  prizes: [],
  rewards: [],
  events: [],
});

const hasLists = (item: HasRoomLists) => item.rooms.length > 0 || item.excludedRooms.length > 0;

/**
 * What a room can switch, from the whole config (the admin page has it). With the room, "on by default" is what the room does today with no
 * choice of its own, old room lists included, so the toggles start from what really runs there.
 */
export function catalogFromConfig(config: GameplayConfig, place: RoomRef | null): RoomToggleCatalog {
  const events = EVENT_TOGGLES.map((event): EventToggleItem => {
    const settings = config.worldEvents[event.key] as { enabled: boolean } & HasRoomLists;
    return {
      id: event.id,
      name: event.id,
      intensity: event.intensity,
      onByDefault: settings.enabled && (place === null || listsAllow(settings, place, false)),
      legacy: hasLists(settings),
    };
  });
  return {
    perksEnabled: config.perks.enabled,
    prizesEnabled: config.prize.enabled,
    worldEventsEnabled: config.worldEvents.enabled,
    perks: config.perks.perks.map((perk) => ({
      id: perk.id,
      name: perk.name || perk.id,
      onByDefault: place === null ? perk.enabled && !perk.script?.trim() : perkWorksInRoom(perk, place),
      legacy: hasLists(perk),
    })),
    prizes: config.prize.prizes.map((prize) => ({
      id: prize.id,
      name: prize.name || prize.id,
      onByDefault: prize.enabled && (place === null || listsAllow(prize, place, false)),
      legacy: hasLists(prize),
    })),
    rewards: (config.rewards ?? []).map((reward) => ({ id: reward.id, name: reward.name || reward.id, onByDefault: true, legacy: false })),
    events,
  };
}

const isObject = (value: unknown): value is Record<string, unknown> => typeof value === "object" && value !== null && !Array.isArray(value);

const itemsOf = (raw: unknown): ToggleItem[] =>
  (Array.isArray(raw) ? raw : []).filter(isObject).flatMap((entry) =>
    typeof entry.id === "string" && entry.id
      ? [{ id: entry.id, name: typeof entry.name === "string" && entry.name ? entry.name : entry.id, onByDefault: entry.onByDefault !== false, legacy: false }]
      : []
  );

/** Reads the public list of what a room can switch (`GET /gameplay/toggles`). Whatever it cannot use is left out. */
export function catalogFromReply(raw: unknown): RoomToggleCatalog {
  if (!isObject(raw)) return emptyCatalog();
  const events = (Array.isArray(raw.events) ? raw.events : []).filter(isObject).flatMap((entry): EventToggleItem[] => {
    const known = EVENT_TOGGLES.find((event) => event.id === entry.id);
    return known ? [{ id: known.id, name: known.id, onByDefault: entry.onByDefault !== false, legacy: false, intensity: known.intensity }] : [];
  });
  return {
    perksEnabled: raw.perksEnabled !== false,
    prizesEnabled: raw.prizesEnabled !== false,
    worldEventsEnabled: raw.worldEventsEnabled !== false,
    perks: itemsOf(raw.perks),
    prizes: itemsOf(raw.prizes),
    rewards: itemsOf(raw.rewards),
    events,
  };
}

export const itemsOnByDefault = (items: ToggleItem[]): number => items.filter((item) => item.onByDefault).length;
