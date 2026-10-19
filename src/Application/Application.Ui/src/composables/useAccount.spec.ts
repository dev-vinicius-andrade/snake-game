import { describe, expect, it, vi } from "vitest";
import { ACCOUNT_TOKEN_STORAGE_KEY, AccountError, useAccount, type AccountHttp, type AccountStorage } from "./useAccount";

function memoryStorage(initial: Record<string, string> = {}): AccountStorage & { data: Record<string, string> } {
  const data = { ...initial };
  return {
    data,
    getItem: (key) => data[key] ?? null,
    setItem: (key, value) => {
      data[key] = value;
    },
    removeItem: (key) => {
      delete data[key];
    },
  };
}

const registered = { username: "Vini", tag: 9291, display: "Vini#09291", token: "tok-1", recoveryPhrase: "amber-otter-nine-cable" };

function setup(reply: (call: { method: string; url: string; body?: unknown; token?: string }) => { status: number; data?: unknown }, stored: Record<string, string> = {}) {
  const storage = memoryStorage(stored);
  const request = vi.fn(async (call: { method: string; url: string; body?: unknown; token?: string }) => ({ data: undefined as unknown, ...reply(call) }));
  const http: AccountHttp = { request };
  const account = useAccount({ managerUrl: "http://manager.test/", http, storage });
  return { account, request, storage };
}

describe("useAccount register", () => {
  it("posts, stores the token, signs in and returns the phrase to show once", async () => {
    const { account, request, storage } = setup(() => ({ status: 201, data: registered }));

    const phrase = await account.register({ username: "Vini", tag: 9291 });

    expect(phrase).toBe("amber-otter-nine-cable");
    expect(request).toHaveBeenCalledWith({ method: "POST", url: "http://manager.test/accounts", body: { username: "Vini", tag: 9291 } });
    expect(storage.data[ACCOUNT_TOKEN_STORAGE_KEY]).toBe("tok-1");
    expect(account.status.value).toBe("signedIn");
    expect(account.account.value).toEqual({ username: "Vini", tag: 9291, display: "Vini#09291" });
    expect(account.error.value).toBeNull();
  });

  it("sends a chosen phrase and leaves out an empty tag", async () => {
    const { account, request } = setup(() => ({ status: 201, data: registered }));

    await account.register({ username: "Vini", phrase: "my own long phrase" });

    expect(request.mock.calls[0][0].body).toEqual({ username: "Vini", phrase: "my own long phrase" });
  });

  it.each([
    [400, "invalidUsername"],
    [400, "invalidTag"],
    [400, "weakPhrase"],
    [409, "noTagsLeft"],
  ])("maps %i %s to a typed error", async (status, code) => {
    const { account, storage } = setup(() => ({ status, data: { error: code } }));

    await expect(account.register({ username: "x" })).rejects.toMatchObject({ code });

    expect(account.status.value).toBe("error");
    expect(account.error.value?.code).toBe(code);
    expect(storage.data[ACCOUNT_TOKEN_STORAGE_KEY]).toBeUndefined();
  });

  it("carries retryAfterSeconds on a throttle", async () => {
    const { account } = setup(() => ({ status: 429, data: { error: "throttled", retryAfterSeconds: 30 } }));

    const failure = await account.register({ username: "x" }).catch((e) => e);

    expect(failure).toBeInstanceOf(AccountError);
    expect(failure).toMatchObject({ code: "throttled", retryAfterSeconds: 30 });
    expect(account.error.value?.retryAfterSeconds).toBe(30);
  });

  it("calls an unknown or network failure unavailable", async () => {
    const storage = memoryStorage();
    const http: AccountHttp = { request: vi.fn().mockRejectedValue(new Error("offline")) };
    const account = useAccount({ managerUrl: "http://m", http, storage });

    await expect(account.register({ username: "x" })).rejects.toMatchObject({ code: "unavailable" });
    expect(account.status.value).toBe("error");
  });
});

describe("useAccount login", () => {
  it("posts the credentials and signs in", async () => {
    const { account, request, storage } = setup(() => ({ status: 200, data: { username: "Vini", tag: 9291, display: "Vini#09291", token: "tok-2" } }));

    await account.login({ username: "Vini", tag: 9291, phrase: "amber-otter-nine-cable" });

    expect(request).toHaveBeenCalledWith({
      method: "POST",
      url: "http://manager.test/accounts/login",
      body: { username: "Vini", tag: 9291, phrase: "amber-otter-nine-cable" },
    });
    expect(storage.data[ACCOUNT_TOKEN_STORAGE_KEY]).toBe("tok-2");
    expect(account.status.value).toBe("signedIn");
  });

  it("maps invalidCredentials and throttled with the wait", async () => {
    const wrong = setup(() => ({ status: 401, data: { error: "invalidCredentials" } }));
    await expect(wrong.account.login({ username: "a", tag: 1, phrase: "b" })).rejects.toMatchObject({ code: "invalidCredentials" });

    const slow = setup(() => ({ status: 429, data: { error: "throttled", retryAfterSeconds: 120 } }));
    await expect(slow.account.login({ username: "a", tag: 1, phrase: "b" })).rejects.toMatchObject({ code: "throttled", retryAfterSeconds: 120 });
  });
});

describe("useAccount loadMe", () => {
  it("stays a guest without a token and makes no call", async () => {
    const { account, request } = setup(() => ({ status: 500 }));

    await account.loadMe();

    expect(request).not.toHaveBeenCalled();
    expect(account.status.value).toBe("guest");
  });

  it("sends the bearer token and signs in", async () => {
    const { account, request } = setup(() => ({ status: 200, data: { id: "1", username: "Vini", tag: 9291, display: "Vini#09291", createdAt: "x" } }), {
      [ACCOUNT_TOKEN_STORAGE_KEY]: "tok-1",
    });

    await account.loadMe();

    expect(request).toHaveBeenCalledWith({ method: "GET", url: "http://manager.test/accounts/me", token: "tok-1" });
    expect(account.status.value).toBe("signedIn");
    expect(account.account.value?.display).toBe("Vini#09291");
  });

  it("clears the token on 401 and becomes a guest without an error", async () => {
    const { account, storage } = setup(() => ({ status: 401, data: { error: "invalidToken" } }), { [ACCOUNT_TOKEN_STORAGE_KEY]: "old" });

    await account.loadMe();

    expect(storage.data[ACCOUNT_TOKEN_STORAGE_KEY]).toBeUndefined();
    expect(account.status.value).toBe("guest");
    expect(account.error.value).toBeNull();
  });

  it("keeps the token when the server is just unreachable", async () => {
    const { account, storage } = setup(() => ({ status: 503 }), { [ACCOUNT_TOKEN_STORAGE_KEY]: "tok" });

    await account.loadMe();

    expect(storage.data[ACCOUNT_TOKEN_STORAGE_KEY]).toBe("tok");
    expect(account.status.value).toBe("error");
    expect(account.error.value?.code).toBe("unavailable");
  });
});

describe("useAccount logout", () => {
  it("revokes the token, forgets it and becomes a guest", async () => {
    const { account, request, storage } = setup((call) => (call.method === "POST" && call.url.endsWith("/accounts") ? { status: 201, data: registered } : { status: 204 }));
    await account.register({ username: "Vini" });

    await account.logout();

    expect(request).toHaveBeenLastCalledWith({ method: "POST", url: "http://manager.test/accounts/logout", token: "tok-1" });
    expect(storage.data[ACCOUNT_TOKEN_STORAGE_KEY]).toBeUndefined();
    expect(account.status.value).toBe("guest");
    expect(account.account.value).toBeNull();
  });

  it("still signs out locally when the call fails", async () => {
    const storage = memoryStorage({ [ACCOUNT_TOKEN_STORAGE_KEY]: "tok" });
    const http: AccountHttp = { request: vi.fn().mockRejectedValue(new Error("offline")) };
    const account = useAccount({ managerUrl: "http://m", http, storage });

    await account.logout();

    expect(storage.data[ACCOUNT_TOKEN_STORAGE_KEY]).toBeUndefined();
    expect(account.status.value).toBe("guest");
  });
});

describe("useAccount checkAvailability", () => {
  it("queries with an encoded username and optional tag, without touching the state", async () => {
    const { account, request } = setup(() => ({ status: 200, data: { available: true, username: "Big Snake" } }));

    const result = await account.checkAvailability("Big Snake", 9291);

    expect(request).toHaveBeenCalledWith({ method: "GET", url: "http://manager.test/accounts/availability?username=Big%20Snake&tag=9291" });
    expect(result).toEqual({ available: true, username: "Big Snake" });
    expect(account.status.value).toBe("guest");
  });

  it("omits the tag when none and throws invalidUsername", async () => {
    const { account, request } = setup(() => ({ status: 400, data: { error: "invalidUsername" } }));

    await expect(account.checkAvailability("#")).rejects.toMatchObject({ code: "invalidUsername" });
    expect(request.mock.calls[0][0].url).toBe("http://manager.test/accounts/availability?username=%23");
  });
});

describe("storage failures", () => {
  it("works when storage throws", async () => {
    const storage: AccountStorage = {
      getItem: () => {
        throw new Error("blocked");
      },
      setItem: () => {
        throw new Error("blocked");
      },
      removeItem: () => {
        throw new Error("blocked");
      },
    };
    const http: AccountHttp = { request: vi.fn(async () => ({ status: 201, data: registered })) };
    const account = useAccount({ managerUrl: "http://m", http, storage });

    await expect(account.loadMe()).resolves.toBeUndefined();
    await expect(account.register({ username: "Vini" })).resolves.toBe("amber-otter-nine-cable");
    expect(account.status.value).toBe("signedIn");
    await expect(account.logout()).resolves.toBeUndefined();
  });
});
