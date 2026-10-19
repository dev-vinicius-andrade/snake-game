import { beforeEach, describe, expect, it, vi } from "vitest";
import { ACCOUNT_TOKEN_STORAGE_KEY, tokenRejected, type AccountCall, type AccountReply, type AccountStorage } from "./useAccount";
import { clearAchievementDefinitionsCache, parseProfile, useProfile } from "./useProfile";

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

const definitions = [{ id: "first-blood", icon: "game-icons:pointy-sword", stat: "kills", threshold: 1 }];
const profileBody = {
  account: { id: "a1", username: "Vini", tag: 9291, display: "Vini#09291", createdAt: "2026-01-01T00:00:00Z" },
  stats: { kills: 3, playSeconds: 90 },
  achievements: [{ id: "first-blood", unlockedAt: "2026-01-02T00:00:00Z" }],
};

function setup(answers: { profile?: AccountReply; list?: AccountReply } = {}, stored: Record<string, string> = { [ACCOUNT_TOKEN_STORAGE_KEY]: "tok-1" }) {
  const storage = memoryStorage(stored);
  const request = vi.fn(async (call: AccountCall): Promise<AccountReply> => {
    if (call.url.endsWith("/achievements")) return answers.list ?? { status: 200, data: definitions };
    return answers.profile ?? { status: 200, data: profileBody };
  });
  const profile = useProfile({ managerUrl: "http://manager.test/", http: { request }, storage });
  return { profile, request, storage };
}

describe("useProfile", () => {
  beforeEach(() => {
    clearAchievementDefinitionsCache();
    tokenRejected.value = false;
  });

  it("loads the profile with the bearer token and the achievement list", async () => {
    const { profile, request } = setup();

    await profile.load();

    expect(request).toHaveBeenCalledWith({ method: "GET", url: "http://manager.test/accounts/me/profile", token: "tok-1" });
    expect(request).toHaveBeenCalledWith({ method: "GET", url: "http://manager.test/achievements" });
    expect(profile.status.value).toBe("ready");
    expect(profile.profile.value?.account.display).toBe("Vini#09291");
    expect(profile.profile.value?.stats.kills).toBe(3);
    expect(profile.profile.value?.achievements).toEqual([{ id: "first-blood", unlockedAt: "2026-01-02T00:00:00Z" }]);
    expect(profile.definitions.value).toEqual(definitions);
  });

  it("is signed out without a token and does not ask for the profile", async () => {
    const { profile, request } = setup({}, {});

    await profile.load();

    expect(profile.status.value).toBe("signedOut");
    expect(request.mock.calls.some(([call]) => call.url.includes("/accounts/me/profile"))).toBe(false);
  });

  it("treats 401 as signed out, forgets the token and notes the rejection", async () => {
    const { profile, storage } = setup({ profile: { status: 401, data: { error: "invalidToken" } } });

    await profile.load();

    expect(profile.status.value).toBe("signedOut");
    expect(profile.profile.value).toBeNull();
    expect(storage.data[ACCOUNT_TOKEN_STORAGE_KEY]).toBeUndefined();
    expect(tokenRejected.value).toBe(true);
  });

  it("reports an error on a failing server", async () => {
    const { profile } = setup({ profile: { status: 500 } });

    await profile.load();

    expect(profile.status.value).toBe("error");
  });

  it("asks for the achievement list once per manager", async () => {
    const first = setup();
    await first.profile.load();
    const second = setup();
    await second.profile.load();

    expect(second.request.mock.calls.some(([call]) => call.url.endsWith("/achievements"))).toBe(false);
    expect(second.profile.definitions.value).toEqual(definitions);
  });

  it("does not keep a failed list", async () => {
    const failing = setup({ list: { status: 500 } });
    await failing.profile.load();
    const retry = setup();
    await retry.profile.load();

    expect(retry.profile.definitions.value).toEqual(definitions);
  });
});

describe("parseProfile", () => {
  it("fills in zero-safe defaults and drops bad entries", () => {
    const parsed = parseProfile({ account: profileBody.account, stats: { kills: "x", foods: 4 }, achievements: [{ id: 3 }, { id: "ok" }] });

    expect(parsed.stats).toEqual({ foods: 4 });
    expect(parsed.achievements).toEqual([{ id: "ok", unlockedAt: "" }]);
  });

  it("rejects an answer without an account", () => {
    expect(() => parseProfile({})).toThrow();
  });
});
