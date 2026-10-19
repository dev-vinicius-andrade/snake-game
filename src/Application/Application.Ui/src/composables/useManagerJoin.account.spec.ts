import axios, { AxiosError } from "axios";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { FakeHubClient } from "@/test/fakeHub";
import { ACCOUNT_TOKEN_STORAGE_KEY, tokenRejected, type AccountStorage } from "./useAccount";
import { InvalidTokenError, useManagerJoin } from "./useManagerJoin";

const assignment = { gameServerUrl: "https://game.example.com/game", roomId: "r1", nickname: "Vini#09291" };

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

describe("useManagerJoin signed in", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    tokenRejected.value = false;
  });
  afterEach(() => {
    vi.useRealTimers();
    vi.restoreAllMocks();
  });

  it("hands the token to the join request", async () => {
    const client = new FakeHubClient("manager");
    const postJoin = vi.fn(async () => client.emit("onJoinedRoom", assignment));
    const storage = memoryStorage({ [ACCOUNT_TOKEN_STORAGE_KEY]: "tok-1" });
    const joiner = useManagerJoin({ managerUrl: "http://manager.test", createClient: () => client, postJoin, storage });

    await joiner.join("Vini#09291");

    expect(postJoin).toHaveBeenCalledWith("http://manager.test/join", { id: "conn-1", nickname: "Vini#09291" }, "tok-1");
  });

  it("sends the bearer header on the real request, and none for a guest", async () => {
    const client = new FakeHubClient("manager");
    const post = vi.spyOn(axios, "post").mockImplementation(async () => {
      client.emit("onJoinedRoom", assignment);
      return { status: 200 };
    });
    const signed = useManagerJoin({
      managerUrl: "http://manager.test",
      createClient: () => client,
      storage: memoryStorage({ [ACCOUNT_TOKEN_STORAGE_KEY]: "tok-1" }),
    });
    await signed.join("Vini#09291");
    expect(post).toHaveBeenLastCalledWith("http://manager.test/join", expect.anything(), { headers: { Authorization: "Bearer tok-1" } });

    const guest = useManagerJoin({ managerUrl: "http://manager.test", createClient: () => client, storage: memoryStorage() });
    await guest.join("ana");
    expect(post).toHaveBeenLastCalledWith("http://manager.test/join", expect.anything());
  });

  it("forgets a token the manager refuses and says so", async () => {
    const client = new FakeHubClient("manager");
    const refusal = new AxiosError("no", "ERR_BAD_REQUEST", undefined, undefined, {
      status: 401,
      data: { error: "invalidToken" },
    } as never);
    vi.spyOn(axios, "post").mockRejectedValue(refusal);
    const storage = memoryStorage({ [ACCOUNT_TOKEN_STORAGE_KEY]: "tok-1" });
    const joiner = useManagerJoin({ managerUrl: "http://manager.test", createClient: () => client, storage });

    await expect(joiner.join("Vini#09291")).rejects.toBeInstanceOf(InvalidTokenError);

    expect(storage.data[ACCOUNT_TOKEN_STORAGE_KEY]).toBeUndefined();
    expect(tokenRejected.value).toBe(true);
    expect(joiner.error.value).toMatch(/session ended/i);
  });

  it("does not take another 401 for a refused token", async () => {
    const client = new FakeHubClient("manager");
    vi.spyOn(axios, "post").mockRejectedValue(
      new AxiosError("no", "ERR_BAD_REQUEST", undefined, undefined, { status: 401, data: {} } as never)
    );
    const storage = memoryStorage({ [ACCOUNT_TOKEN_STORAGE_KEY]: "tok-1" });
    const joiner = useManagerJoin({ managerUrl: "http://manager.test", createClient: () => client, storage });

    await expect(joiner.join("Vini#09291")).rejects.not.toBeInstanceOf(InvalidTokenError);
    expect(storage.data[ACCOUNT_TOKEN_STORAGE_KEY]).toBe("tok-1");
  });
});
