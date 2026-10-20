import { readStoredAccountToken } from "./useAccount";

/** What the admin page signs in with when it is not a key: the account the player is signed in with, which has the admin role (docs/ROLES.md). */
export const ACCOUNT_CREDENTIAL = "@account";

const ADMIN_KEY_HEADER = "X-Admin-Key";

/** The headers of an admin request: the admin key, or the device token of the signed in admin account. */
export function adminHeaders(credential: string, accountToken: string | null = readStoredAccountToken()): Record<string, string> {
  if (credential !== ACCOUNT_CREDENTIAL) return { [ADMIN_KEY_HEADER]: credential };
  return accountToken ? { Authorization: `Bearer ${accountToken}` } : {};
}

/** The server refused the credential: not signed in (401), or signed in without the admin role (403). */
export const isRejected = (status: number | undefined): boolean => status === 401 || status === 403;
