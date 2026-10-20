/** Why a sign in with a social account can end without an account; the manager sends the code back in the address. */
export const OAUTH_ERRORS = ["cancelled", "invalidState", "providerFailed", "unknownProvider", "invalidTicket", "throttled", "noTagsLeft", "invalidCredentials"] as const;

export type OAuthError = (typeof OAUTH_ERRORS)[number];

export type OAuthOutcome = { kind: "ticket"; ticket: string } | { kind: "error"; error: OAuthError } | { kind: "none" };

type QueryValue = string | null | Array<string | null> | undefined;

const first = (value: QueryValue): string => (Array.isArray(value) ? (value[0] ?? "") : (value ?? "")).trim();

/** What the address says after the browser came back from a social sign in: a ticket to trade, or why it failed. */
export function readOAuthOutcome(query: Record<string, QueryValue>): OAuthOutcome {
  const ticket = first(query.ticket);
  if (ticket) return { kind: "ticket", ticket };
  const error = first(query.error);
  if (!error) return { kind: "none" };
  return { kind: "error", error: (OAUTH_ERRORS as readonly string[]).includes(error) ? (error as OAuthError) : "providerFailed" };
}

const ICONS: Readonly<Record<string, string>> = { github: "mdi-github", discord: "mdi-discord", google: "mdi-google", steam: "mdi-steam" };

export const providerIcon = (id: string): string => ICONS[id] ?? "mdi-account-circle";

/** How a provider's button looks: the colours of the provider's own brand, so a player knows at a glance which account it is. */
export interface ProviderLook {
  background: string;
  text: string;
  border: string;
  hover: string;
}

const LOOKS: Readonly<Record<string, ProviderLook>> = {
  // Black with white text, like GitHub's own button on a dark page.
  github: { background: "#000000", text: "#ffffff", border: "#3d444d", hover: "#24292f" },
  // Discord's "blurple" with white text.
  discord: { background: "#5865f2", text: "#ffffff", border: "#5865f2", hover: "#4752c4" },
  // Google's sign in button: white, dark text and a thin border, with the coloured G.
  google: { background: "#ffffff", text: "#1f1f1f", border: "#dadce0", hover: "#f1f3f4" },
  // Steam's dark blue.
  steam: { background: "#171a21", text: "#ffffff", border: "#66c0f4", hover: "#1b2838" },
};

const NEUTRAL_LOOK: ProviderLook = { background: "#2c3558", text: "#ffffff", border: "#4a5688", hover: "#3a4570" };

export const providerLook = (id: string): ProviderLook => LOOKS[id] ?? NEUTRAL_LOOK;

function luminance(hex: string): number {
  const channels = [1, 3, 5].map((start) => {
    const value = parseInt(hex.slice(start, start + 2), 16) / 255;
    return value <= 0.03928 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2];
}

/** WCAG contrast ratio of two `#rrggbb` colours, 1 (the same) to 21 (black on white). */
export function contrastRatio(first: string, second: string): number {
  const [light, dark] = [luminance(first), luminance(second)].sort((a, b) => b - a);
  return (light + 0.05) / (dark + 0.05);
}
