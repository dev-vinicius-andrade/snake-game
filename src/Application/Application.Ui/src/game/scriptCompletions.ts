/** What a script can ask and answer, for the editor's suggestions. Kept apart from the editor so it can be tested. */

export interface ScriptCompletion {
  label: string;
  type: "property" | "function" | "keyword";
  detail?: string;
  apply?: string;
}

export interface ScriptCompletions {
  /** Where in the text before the cursor the word being typed starts. */
  from: number;
  options: ScriptCompletion[];
}

const prop = (label: string, detail: string): ScriptCompletion => ({ label, type: "property", detail });

/** What each name a function receives holds. */
export const SCRIPT_MEMBERS: Readonly<Record<string, ScriptCompletion[]>> = {
  snake: [
    prop("name", "text"),
    prop("length", "segments"),
    prop("mass", "points"),
    prop("foodsEaten", "foods eaten in this life"),
    prop("health", "0..1, average of the body"),
    prop("lowestHealth", "0..1, the worst segment"),
    prop("boosting", "true while boosting"),
    prop("perks", "ids of its perks"),
    prop("now", "ms, the server clock"),
    prop("ageMs", "ms since it came in"),
    prop("kills", "kills in this life"),
    prop("state", "remembered between calls, per snake"),
  ],
  room: [
    prop("players", "players in the room"),
    prop("name", "text"),
    prop("code", "invite code"),
    prop("width", "world units"),
    prop("height", "world units"),
    prop("now", "ms, the server clock"),
    prop("state", "onTick only: remembered, per room"),
  ],
  food: [prop("value", "mass it gives"), prop("isPrize", "true for a prize")],
  victim: [prop("name", "text"), prop("length", "segments"), prop("mass", "points")],
};

const MODIFY_KEYS = [
  prop("extraShots", "0..4 more shots per volley"),
  prop("burstExtraShots", "0..6 shots after the first"),
  prop("boostSpeedBonus", "0..3"),
  prop("boostLengthFactor", "1..4, longer boost"),
  prop("reloadFactor", "0.2..1, lower is faster"),
  prop("shotSpeedFactor", "1..4"),
  prop("damageFactor", "0.2..1, share of damage taken"),
  prop("regenPerSecond", "0..50 HP"),
  prop("magnetRadius", "0..300"),
  prop("autoFire", "true or false"),
  prop("piercing", "true or false"),
  prop("invulnerable", "true or false"),
  prop("freeShots", "true or false"),
];

const REWARD_KEYS = [prop("mass", "0..50 extra mass"), prop("heal", "0..500 HP")];

/** The keys a function may return, by the function. */
export const SCRIPT_RETURNS: Readonly<Record<string, ScriptCompletion[]>> = {
  modify: MODIFY_KEYS,
  onEat: REWARD_KEYS,
  onKill: REWARD_KEYS,
  onTick: [prop("dropPrize", "a prize kind id, or a list of them")],
};

/** The functions a script can define, with the lines that start one. */
export const SCRIPT_HOOKS: readonly ScriptCompletion[] = [
  { label: "modify", type: "function", detail: "(snake, room) extra effects", apply: "modify(snake, room) {\n  return {};\n}" },
  { label: "onEat", type: "function", detail: "(snake, food, room) mass, heal", apply: "onEat(snake, food, room) {\n  return {};\n}" },
  { label: "onKill", type: "function", detail: "(snake, victim, room) mass, heal", apply: "onKill(snake, victim, room) {\n  return {};\n}" },
  { label: "onTick", type: "function", detail: "(room) once a second, may drop a prize", apply: "onTick(room) {\n  return {};\n}" },
];

/** The suggestions for the text before the cursor, or null when there is nothing to suggest there. */
export function completionsFor(before: string): ScriptCompletions | null {
  return memberOf(before) ?? returnKeyOf(before) ?? hookOf(before);
}

function memberOf(before: string): ScriptCompletions | null {
  const found = /\b(snake|room|food|victim)\.(\w*)$/.exec(before);
  if (!found) return null;
  return { from: before.length - found[2].length, options: SCRIPT_MEMBERS[found[1]] };
}

function returnKeyOf(before: string): ScriptCompletions | null {
  const start = before.lastIndexOf("return {");
  if (start < 0) return null;
  const inside = before.slice(start + "return {".length);
  const word = /(?:^|[,{\s])(\w*)$/.exec(inside);
  const writingAValue = inside.includes(":") && !/,\s*\w*$/.test(inside);
  if (inside.includes("}") || writingAValue || !word) return null;
  const hook = [...before.slice(0, start).matchAll(/function\s+(\w+)/g)].pop()?.[1] ?? "";
  const keys = SCRIPT_RETURNS[hook];
  if (!keys) return null;
  return { from: before.length - word[1].length, options: keys.map((key) => ({ ...key, apply: `${key.label}: ` })) };
}

function hookOf(before: string): ScriptCompletions | null {
  const found = /\bfunction\s+(\w*)$/.exec(before);
  return found ? { from: before.length - found[1].length, options: [...SCRIPT_HOOKS] } : null;
}
