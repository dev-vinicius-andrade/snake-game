import gameIconNames from "./gameIconNames.json";
import { GAME_ICON_PREFIX } from "./gameIcons";

/** The effects a perk can have, as the server names them. */
export const PERK_EFFECTS = [
  "ExtraShots",
  "BoostSpeed",
  "FasterReload",
  "EfficientBoost",
  "BurstShot",
  "ShotSpeed",
  "AutoFire",
  "PiercingShot",
  "Armor",
  "Regeneration",
  "Magnet",
] as const;

const ICONS: Readonly<Record<string, string>> = {
  ExtraShots: "game-icons:arrow-cluster",
  BoostSpeed: "game-icons:sprint",
  FasterReload: "game-icons:reload-gun-barrel",
  EfficientBoost: "game-icons:lungs",
  BurstShot: "game-icons:bullets",
  ShotSpeed: "game-icons:fast-arrow",
  AutoFire: "game-icons:cycle",
  PiercingShot: "game-icons:arrow-dunk",
  Armor: "game-icons:checked-shield",
  Regeneration: "game-icons:health-increase",
  Magnet: "game-icons:magnet",
};

/** The icon of an effect; an effect this client does not know yet still gets one. */
export const perkIcon = (effect: string): string => ICONS[effect] ?? "game-icons:sparkles";

/** Icons an admin can pick for a perk: the game icons the game ships. Any Material Design Icons name (mdi-...) can still be typed. */
export const PERK_ICON_CHOICES: readonly string[] = gameIconNames.map((name) => `${GAME_ICON_PREFIX}${name}`);

const ICON_NAME = /^(mdi-[a-z0-9]+(-[a-z0-9]+)*|game-icons:[a-z0-9]+(-[a-z0-9]+)*)$/;

interface IconSource {
  icon?: string | null;
  effects: { effect: string }[];
}

/** The icon a perk is shown with: the one it was given (a game icon or a Material Design icon), else the icon of its first effect. */
export const perkIconOf = (perk: IconSource): string => (perk.icon && ICON_NAME.test(perk.icon) ? perk.icon : perkIcon(perk.effects[0]?.effect ?? ""));

/** Where the sentence that says what an effect does is kept in the language files. */
export const perkTooltipKey = (effect: string): string => `perkEffects.${effect}.tooltip`;

interface PerkLike {
  effect: string;
  amount: number;
  foodsRequired: number;
}

const rounded = (value: number, decimals = 2) => Math.round(value * 10 ** decimals) / 10 ** decimals;

/** The effects whose amount is a share, which the game never lets a tier push past this. */
const RATIO_EFFECTS = ["FasterReload", "EfficientBoost", "Armor"];
const MAX_SCALED_RATIO = 0.95;

/** The numbers a tooltip talks about: the amount, the same as a percent, the shots in a volley or burst, the foods needed. A tier scales the amount. */
export function perkParams(perk: PerkLike, multiplier = 1) {
  const scaled = RATIO_EFFECTS.includes(perk.effect) ? Math.min(MAX_SCALED_RATIO, perk.amount * multiplier) : perk.amount * multiplier;
  return {
    amount: rounded(scaled),
    percent: Math.round(scaled * 100),
    count: Math.round(scaled) + 1,
    foods: perk.foodsRequired,
  };
}

interface Tiered {
  foodsRequired: number;
  tiers?: Array<{ foodsRequired: number; multiplier: number }>;
}

/** The roman numeral of a tier, for the badge on a perk. */
export const tierLabel = (tier: number): string => ["I", "II", "III", "IV", "V"][tier - 1] ?? String(tier);

/** What a tier of a perk multiplies its amounts by (1 for the first) and how many foods the next tier asks for (null at the top). */
export function tierInfo(perk: Tiered, tier: number): { multiplier: number; nextFoods: number | null } {
  const steps = [...(perk.tiers ?? [])].sort((first, second) => first.foodsRequired - second.foodsRequired);
  const at = Math.min(Math.max(tier, 1), steps.length + 1);
  return { multiplier: at === 1 ? 1 : steps[at - 2].multiplier, nextFoods: steps[at - 1]?.foodsRequired ?? null };
}
