/** Accepts "09291", "9291" or "#09291" (1 to 5 digits). Anything else is null. */
export function parseTag(input: string | null | undefined): number | null {
  const match = /^#?(\d{1,5})$/.exec((input ?? "").trim());
  return match ? Number(match[1]) : null;
}

export function formatTag(tag: number): string {
  return String(tag).padStart(5, "0");
}

export function formatDisplay(username: string, tag: number): string {
  return `${username}#${formatTag(tag)}`;
}

/** A guest may not pose as an account: no nickname ending in '#' and digits. */
export function isGuestNicknameAllowed(nickname: string): boolean {
  return !/#\d+$/.test(nickname.trim());
}

/** Client side hint only; the server decides what is valid. */
export function cleanUsername(input: string): string {
  return input.trim().replace(/\s+/g, " ");
}
