/**
 * What a room does with each feature of the gameplay, over what the administrator set for every room. `inherit` is the default:
 * the room changes nothing. Perks and prizes only know off and on (`normal`); the world events also have calm and wild.
 */
export type Preset = "inherit" | "off" | "calm" | "normal" | "wild";

export type RoomGameplayChoices = Record<string, Preset>;

const ON_OFF: readonly Preset[] = ["inherit", "off", "normal"];
const EVENT_PRESETS: readonly Preset[] = ["inherit", "off", "calm", "normal", "wild"];

/** The features a room can choose about, in the order they are shown, with the presets each offers. The names are the server's. */
export const GAMEPLAY_FEATURES: ReadonlyArray<{ key: string; presets: readonly Preset[] }> = [
  { key: "perks", presets: ON_OFF },
  { key: "prizes", presets: ON_OFF },
  { key: "lightning", presets: EVENT_PRESETS },
  { key: "meteors", presets: EVENT_PRESETS },
  { key: "turret", presets: EVENT_PRESETS },
  { key: "zone", presets: EVENT_PRESETS },
];

export const presetsOf = (feature: string): readonly Preset[] => GAMEPLAY_FEATURES.find((candidate) => candidate.key === feature)?.presets ?? ["inherit"];

/** A room that changes nothing. */
export const defaultChoices = (): RoomGameplayChoices => Object.fromEntries(GAMEPLAY_FEATURES.map((feature) => [feature.key, "inherit" as Preset]));

export const isDefaultChoices = (choices: RoomGameplayChoices): boolean => Object.values(choices).every((preset) => preset === "inherit");

/** What to send to the manager: only what differs from the default, or nothing at all. */
export function choicesBody(choices: RoomGameplayChoices): Record<string, string> | undefined {
  const changed = Object.entries(choices).filter(([, preset]) => preset !== "inherit");
  return changed.length === 0 ? undefined : Object.fromEntries(changed);
}

/** Reads what the manager says a room does. Anything it does not understand is left at the default. */
export function parseChoices(raw: unknown): RoomGameplayChoices {
  const choices = defaultChoices();
  if (typeof raw !== "object" || raw === null || Array.isArray(raw)) return choices;
  for (const feature of GAMEPLAY_FEATURES) {
    const value = (raw as Record<string, unknown>)[feature.key];
    const preset = feature.presets.find((candidate) => candidate === value);
    if (preset) choices[feature.key] = preset;
  }
  return choices;
}
