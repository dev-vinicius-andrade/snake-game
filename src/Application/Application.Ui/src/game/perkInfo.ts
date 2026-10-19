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
  ExtraShots: "mdi-source-branch",
  BoostSpeed: "mdi-lightning-bolt",
  FasterReload: "mdi-timer-sand",
  EfficientBoost: "mdi-lungs",
  BurstShot: "mdi-chevron-triple-right",
  ShotSpeed: "mdi-rocket-launch",
  AutoFire: "mdi-autorenew",
  PiercingShot: "mdi-arrow-collapse-right",
  Armor: "mdi-shield",
  Regeneration: "mdi-heart-plus",
  Magnet: "mdi-magnet",
};

/** The icon of an effect; an effect this client does not know yet still gets one. */
export const perkIcon = (effect: string): string => ICONS[effect] ?? "mdi-star-four-points";

/** Icons an admin can pick for a perk. Any other Material Design Icons name (mdi-...) can be typed. */
export const PERK_ICON_CHOICES: readonly string[] = [
  "mdi-shield",
  "mdi-shield-star",
  "mdi-shield-sword",
  "mdi-sword-cross",
  "mdi-skull",
  "mdi-skull-crossbones",
  "mdi-flash",
  "mdi-lightning-bolt",
  "mdi-weather-lightning",
  "mdi-fire",
  "mdi-snowflake",
  "mdi-water",
  "mdi-leaf",
  "mdi-heart",
  "mdi-heart-plus",
  "mdi-star",
  "mdi-star-four-points",
  "mdi-crown",
  "mdi-diamond-stone",
  "mdi-trophy",
  "mdi-target",
  "mdi-crosshairs",
  "mdi-bullseye-arrow",
  "mdi-rocket-launch",
  "mdi-magnet",
  "mdi-eye",
  "mdi-ghost",
  "mdi-timer-sand",
  "mdi-clock-fast",
  "mdi-speedometer",
  "mdi-gift",
  "mdi-bomb",
  "mdi-dice-5",
  "mdi-clover",
  "mdi-paw",
  "mdi-flag-checkered",
];

const ICON_NAME = /^mdi-[a-z0-9]+(-[a-z0-9]+)*$/;

interface IconSource {
  icon?: string | null;
  effects: { effect: string }[];
}

/** The icon a perk is shown with: the one it was given, else the icon of its first effect. */
export const perkIconOf = (perk: IconSource): string => (perk.icon && ICON_NAME.test(perk.icon) ? perk.icon : perkIcon(perk.effects[0]?.effect ?? ""));

/** Where the sentence that says what an effect does is kept in the language files. */
export const perkTooltipKey = (effect: string): string => `perkEffects.${effect}.tooltip`;

interface PerkLike {
  effect: string;
  amount: number;
  foodsRequired: number;
}

const rounded = (value: number, decimals = 2) => Math.round(value * 10 ** decimals) / 10 ** decimals;

/** The numbers a tooltip talks about: the amount, the same as a percent, the shots in a volley or burst, the foods needed. */
export function perkParams(perk: PerkLike) {
  return {
    amount: rounded(perk.amount),
    percent: Math.round(perk.amount * 100),
    count: Math.round(perk.amount) + 1,
    foods: perk.foodsRequired,
  };
}
