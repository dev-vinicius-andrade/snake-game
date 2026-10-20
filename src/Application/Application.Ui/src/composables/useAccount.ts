import axios from "axios";
import { shallowRef } from "vue";
import { resolveManagerUrl } from "@/config/env";

export const ACCOUNT_TOKEN_STORAGE_KEY = "snake.accountToken";

export type AccountStatus = "guest" | "loading" | "signedIn" | "error";

/** The error codes of the manager contract (docs/ACCOUNTS.md), plus `unavailable` for anything else (network, 5xx, odd answer). */
export type AccountErrorCode =
  | "invalidUsername"
  | "invalidDisplayName"
  | "invalidTag"
  | "weakPhrase"
  | "noTagsLeft"
  | "throttled"
  | "invalidCredentials"
  | "invalidToken"
  | "unavailable";

const KNOWN_CODES: readonly AccountErrorCode[] = [
  "invalidUsername",
  "invalidDisplayName",
  "invalidTag",
  "weakPhrase",
  "noTagsLeft",
  "throttled",
  "invalidCredentials",
  "invalidToken",
];

export class AccountError extends Error {
  constructor(
    public readonly code: AccountErrorCode,
    /** Only on `throttled`: seconds to wait before trying again. */
    public readonly retryAfterSeconds?: number
  ) {
    super(code);
    this.name = "AccountError";
  }
}

export interface AccountInfo {
  username: string;
  tag: number;
  /** `Name#09291`: how the owner signs in. Private: never show it to other players, it tells them which account to go after. */
  display: string;
  /** What other players see in the game and on the ranking. The username unless the player chose another name. */
  displayName: string;
}

export interface AccountCall {
  method: "GET" | "POST" | "PUT";
  url: string;
  body?: unknown;
  /** Sent as `Authorization: Bearer`. */
  token?: string;
}

export interface AccountReply {
  status: number;
  data?: unknown;
}

/** One request; resolves with any HTTP status and rejects only when nothing came back. */
export interface AccountHttp {
  request(call: AccountCall): Promise<AccountReply>;
}

export interface AccountStorage {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

export interface UseAccountOptions {
  managerUrl?: string;
  http?: AccountHttp;
  storage?: AccountStorage;
}

export interface RegisterInput {
  username: string;
  tag?: number;
  phrase?: string;
  /** The name other players see; left out, the username is shown. */
  displayName?: string;
}

export interface LoginInput {
  username: string;
  tag: number;
  phrase: string;
}

export interface Availability {
  available: boolean;
  username: string;
}

export const axiosHttp: AccountHttp = {
  async request({ method, url, body, token }) {
    const response = await axios.request({
      method,
      url,
      data: body,
      headers: token ? { Authorization: `Bearer ${token}` } : undefined,
      validateStatus: () => true,
    });
    return { status: response.status, data: response.data };
  },
};

function defaultStorage(): AccountStorage | undefined {
  try {
    return typeof localStorage === "undefined" ? undefined : localStorage;
  } catch {
    return undefined;
  }
}

/** The device token kept by a past sign in, or null. Other composables (join, profile) read it from here. */
export function readStoredAccountToken(storage: AccountStorage | undefined = defaultStorage()): string | null {
  try {
    return storage?.getItem(ACCOUNT_TOKEN_STORAGE_KEY) || null;
  } catch {
    return null;
  }
}

export function clearStoredAccountToken(storage: AccountStorage | undefined = defaultStorage()) {
  try {
    storage?.removeItem(ACCOUNT_TOKEN_STORAGE_KEY);
  } catch {
    // Nothing to clear without storage.
  }
}

/** True after the manager refused the stored token (a join or the profile said invalidToken); cleared by the next sign in. */
export const tokenRejected = shallowRef(false);

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null ? (value as Record<string, unknown>) : {};
}

function toError(reply: AccountReply): AccountError {
  const body = asRecord(reply.data);
  const code = KNOWN_CODES.find((known) => known === body.error);
  if (!code) return new AccountError("unavailable");
  const wait = typeof body.retryAfterSeconds === "number" ? body.retryAfterSeconds : undefined;
  return new AccountError(code, code === "throttled" ? wait : undefined);
}

function toInfo(data: unknown): AccountInfo {
  const { username, tag, display, displayName } = asRecord(data);
  if (typeof username !== "string" || typeof tag !== "number" || typeof display !== "string") throw new AccountError("unavailable");
  return { username, tag, display, displayName: typeof displayName === "string" && displayName.trim() ? displayName : username };
}

export function useAccount(options: UseAccountOptions = {}) {
  const http = options.http ?? axiosHttp;
  const storage = options.storage ?? defaultStorage();
  const base = (options.managerUrl ?? resolveManagerUrl()).replace(/\/+$/, "");

  const status = shallowRef<AccountStatus>("guest");
  const account = shallowRef<AccountInfo | null>(null);
  const error = shallowRef<AccountError | null>(null);
  // Kept in memory too, so a page without storage still works until it is reloaded.
  let token: string | null = null;

  function readToken(): string | null {
    if (token) return token;
    try {
      return storage?.getItem(ACCOUNT_TOKEN_STORAGE_KEY) || null;
    } catch {
      return null;
    }
  }

  function writeToken(value: string | null) {
    token = value;
    try {
      if (value) storage?.setItem(ACCOUNT_TOKEN_STORAGE_KEY, value);
      else storage?.removeItem(ACCOUNT_TOKEN_STORAGE_KEY);
    } catch {
      // The page works without storage.
    }
  }

  async function send(call: AccountCall): Promise<AccountReply> {
    try {
      return await http.request(call);
    } catch {
      throw new AccountError("unavailable");
    }
  }

  function record(failure: unknown): AccountError {
    const typed = failure instanceof AccountError ? failure : new AccountError("unavailable");
    error.value = typed;
    status.value = "error";
    return typed;
  }

  function fail(failure: unknown): never {
    throw record(failure);
  }

  function signIn(data: unknown) {
    const issued = asRecord(data).token;
    if (typeof issued !== "string" || !issued) throw new AccountError("unavailable");
    account.value = toInfo(data);
    writeToken(issued);
    tokenRejected.value = false;
    error.value = null;
    status.value = "signedIn";
  }

  /** Makes the account; resolves with the recovery phrase to show the player once. */
  async function register(input: RegisterInput): Promise<string> {
    status.value = "loading";
    error.value = null;
    try {
      const body: RegisterInput = { username: input.username };
      if (input.tag !== undefined) body.tag = input.tag;
      if (input.phrase) body.phrase = input.phrase;
      if (input.displayName?.trim()) body.displayName = input.displayName.trim();
      const reply = await send({ method: "POST", url: `${base}/accounts`, body });
      if (reply.status !== 201) throw toError(reply);
      const phrase = asRecord(reply.data).recoveryPhrase;
      if (typeof phrase !== "string") throw new AccountError("unavailable");
      signIn(reply.data);
      return phrase;
    } catch (failure) {
      return fail(failure);
    }
  }

  async function login(input: LoginInput): Promise<void> {
    status.value = "loading";
    error.value = null;
    try {
      const reply = await send({
        method: "POST",
        url: `${base}/accounts/login`,
        body: { username: input.username, tag: input.tag, phrase: input.phrase },
      });
      if (reply.status !== 200) throw toError(reply);
      signIn(reply.data);
    } catch (failure) {
      fail(failure);
    }
  }

  /** On start: with a stored token, finds out who it is. A rejected token is forgotten. */
  async function loadMe(): Promise<void> {
    const stored = readToken();
    if (!stored) return;
    status.value = "loading";
    error.value = null;
    try {
      const reply = await send({ method: "GET", url: `${base}/accounts/me`, token: stored });
      if (reply.status === 401) {
        writeToken(null);
        account.value = null;
        status.value = "guest";
        return;
      }
      if (reply.status !== 200) throw toError(reply);
      account.value = toInfo(reply.data);
      status.value = "signedIn";
    } catch (failure) {
      record(failure); // On start nobody is waiting to catch it: the state tells.
    }
  }

  /** Revokes this device's token (best effort) and always forgets it locally. */
  async function logout(): Promise<void> {
    const stored = readToken();
    writeToken(null);
    account.value = null;
    error.value = null;
    status.value = "guest";
    if (!stored) return;
    try {
      await http.request({ method: "POST", url: `${base}/accounts/logout`, token: stored });
    } catch {
      // Signed out here regardless.
    }
  }

  /**
   * Changes the name other players see; blank goes back to the username. Throws the reason when it is refused and leaves the account as it
   * was. A token the manager no longer knows signs this device out.
   */
  async function changeDisplayName(name: string): Promise<void> {
    const stored = readToken();
    if (!stored) throw new AccountError("invalidToken");
    const reply = await send({ method: "PUT", url: `${base}/accounts/me/display-name`, body: { displayName: name.trim() }, token: stored });
    if (reply.status === 401) {
      writeToken(null);
      account.value = null;
      status.value = "guest";
      throw new AccountError("invalidToken");
    }
    if (reply.status !== 200) throw toError(reply);
    account.value = toInfo(reply.data);
  }

  /** Does not change the state. Debounce is the caller's job. */
  async function checkAvailability(username: string, tag?: number): Promise<Availability> {
    const query = `username=${encodeURIComponent(username)}${tag === undefined ? "" : `&tag=${tag}`}`;
    const reply = await send({ method: "GET", url: `${base}/accounts/availability?${query}` });
    if (reply.status !== 200) throw toError(reply);
    const { available, username: kept } = asRecord(reply.data);
    if (typeof available !== "boolean") throw new AccountError("unavailable");
    return { available, username: typeof kept === "string" ? kept : username };
  }

  return { status, account, error, register, login, loadMe, logout, checkAvailability, changeDisplayName };
}
