export const INVITE_CODE_LENGTH = 6;

/** Same letters and digits the manager hands out: no 0, O, 1, I or L, which look alike. */
const VALID_CODE = /^[ABCDEFGHJKMNPQRSTUVWXYZ23456789]{6}$/;

/** Makes what a player typed or pasted into a code: the code from an invite link, or the letters and digits, in upper case. */
export function normalizeInviteCode(input: string): string {
  const lastSegment = input.includes("/") ? (input.split("/").filter((part) => part.trim()).pop() ?? "") : input;
  return lastSegment.replace(/[^a-zA-Z0-9]/g, "").toUpperCase();
}

export function isValidInviteCode(code: string): boolean {
  return VALID_CODE.test(code);
}

export function inviteLink(origin: string, code: string): string {
  return `${origin.replace(/\/+$/, "")}/join/${code}`;
}
