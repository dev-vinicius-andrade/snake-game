export type AdminAccess = "checking" | "granted" | "forbidden";

/**
 * What the admin page shows: nothing yet while it finds out who is there, the admin tools for someone the admin api let in (an account with the
 * admin role, or an admin key), and the forbidden page for anybody else.
 */
export function adminAccess(state: { checking: boolean; signedInToAdmin: boolean }): AdminAccess {
  if (state.signedInToAdmin) return "granted";
  return state.checking ? "checking" : "forbidden";
}
