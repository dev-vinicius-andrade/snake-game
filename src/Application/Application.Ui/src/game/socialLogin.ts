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

const ICONS: Readonly<Record<string, string>> = { github: "mdi-github", discord: "mdi-discord", google: "mdi-google" };

export const providerIcon = (id: string): string => ICONS[id] ?? "mdi-account-circle";
