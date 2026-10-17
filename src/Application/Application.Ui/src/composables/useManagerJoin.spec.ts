import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { FakeHubClient } from "@/test/fakeHub";
import { useManagerJoin } from "./useManagerJoin";

const assignment = {
  gameServerUrl: "https://game.example.com/game",
  roomId: "55555555-5555-5555-5555-555555555555",
  nickname: "ana",
};

function setup(options: { postJoin?: (url: string, body: unknown) => Promise<void>; timeoutMs?: number } = {}) {
  const client = new FakeHubClient("manager");
  const postJoin = vi.fn(options.postJoin ?? (async () => undefined));
  const createClient = vi.fn(() => client);
  const joiner = useManagerJoin({
    managerUrl: "http://manager.test/",
    createClient,
    postJoin,
    timeoutMs: options.timeoutMs,
  });
  return { client, postJoin, createClient, joiner };
}

describe("useManagerJoin", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it("connects to the manager hub, posts the join and resolves with the assignment", async () => {
    const { client, postJoin, createClient, joiner } = setup({
      postJoin: async () => client.emit("onJoinedRoom", assignment),
    });

    const result = await joiner.join("ana");

    expect(createClient).toHaveBeenCalledWith("http://manager.test/manager");
    expect(postJoin).toHaveBeenCalledWith("http://manager.test/join", { id: "conn-1", nickname: "ana" });
    expect(result).toEqual(assignment);
  });

  it("registers the onJoinedRoom handler before starting the connection", async () => {
    const { client, joiner } = setup({ postJoin: async () => client.emit("onJoinedRoom", assignment) });

    await joiner.join("ana");

    expect(client.log.indexOf("on:onJoinedRoom")).toBeLessThan(client.log.indexOf("start"));
  });

  it("joins a specific room through the room route", async () => {
    const { client, postJoin, joiner } = setup({ postJoin: async () => client.emit("onJoinedRoom", assignment) });

    await joiner.join("ana", assignment.roomId);

    expect(postJoin).toHaveBeenCalledWith(`http://manager.test/join/room/${assignment.roomId}`, {
      id: "conn-1",
      nickname: "ana",
    });
  });

  it("accepts the assignment arriving before the post resolves", async () => {
    const { client, joiner } = setup({
      postJoin: async () => {
        client.emit("onJoinedRoom", assignment);
        await Promise.resolve();
      },
    });

    await expect(joiner.join("ana")).resolves.toEqual(assignment);
  });

  it("accepts the assignment arriving after the post resolved", async () => {
    const { client, joiner } = setup();

    const pending = joiner.join("ana");
    await vi.advanceTimersByTimeAsync(10);
    client.emit("onJoinedRoom", assignment);

    await expect(pending).resolves.toEqual(assignment);
  });

  it("closes the manager connection once it has the assignment", async () => {
    const { client, joiner } = setup({ postJoin: async () => client.emit("onJoinedRoom", assignment) });

    await joiner.join("ana");

    expect(client.log).toContain("stop");
    expect(client.handlerCount("onJoinedRoom")).toBe(0);
  });

  it("reports progress through status", async () => {
    const { client, joiner } = setup({ postJoin: async () => client.emit("onJoinedRoom", assignment) });
    expect(joiner.status.value).toBe("idle");

    await joiner.join("ana");

    expect(joiner.status.value).toBe("assigned");
  });

  it("rejects when no assignment arrives in time and cleans up", async () => {
    const { client, joiner } = setup({ timeoutMs: 1000 });

    const pending = joiner.join("ana");
    const failed = expect(pending).rejects.toThrow(/timed out/i);
    await vi.advanceTimersByTimeAsync(1001);

    await failed;
    expect(joiner.status.value).toBe("error");
    expect(joiner.error.value).toMatch(/timed out/i);
    expect(client.log).toContain("stop");
  });

  it("rejects when the post fails", async () => {
    const { client, joiner } = setup({
      postJoin: async () => {
        throw new Error("500");
      },
    });

    await expect(joiner.join("ana")).rejects.toThrow("500");
    expect(client.log).toContain("stop");
    expect(joiner.status.value).toBe("error");
  });

  it("rejects when the hub cannot start", async () => {
    const { client, postJoin, joiner } = setup();
    client.startError = new Error("no route");

    await expect(joiner.join("ana")).rejects.toThrow("no route");
    expect(postJoin).not.toHaveBeenCalled();
    expect(client.log).toContain("stop");
  });

  it("rejects when the hub gives no connection id", async () => {
    const { client, postJoin, joiner } = setup();
    client.nextConnectionId = null as unknown as string;

    await expect(joiner.join("ana")).rejects.toThrow(/connection id/i);
    expect(postJoin).not.toHaveBeenCalled();
  });

  it.each([
    [{ gameServerUrl: "", roomId: assignment.roomId, nickname: "ana" }],
    [{ gameServerUrl: assignment.gameServerUrl, nickname: "ana" }],
    [null],
    ["garbage"],
  ])("rejects an invalid assignment %j", async (payload) => {
    const { client, joiner } = setup({ postJoin: async () => client.emit("onJoinedRoom", payload) });

    await expect(joiner.join("ana")).rejects.toThrow(/invalid assignment/i);
  });

  it.each(["", "   "])("rejects the nickname %j before connecting", async (nickname) => {
    const { createClient, joiner } = setup();

    await expect(joiner.join(nickname)).rejects.toThrow(/nickname/i);
    expect(createClient).not.toHaveBeenCalled();
  });

  it("trims the nickname", async () => {
    const { client, postJoin, joiner } = setup({ postJoin: async () => client.emit("onJoinedRoom", assignment) });

    await joiner.join("  ana  ");

    expect(postJoin).toHaveBeenCalledWith(expect.any(String), { id: "conn-1", nickname: "ana" });
  });

  it("cancel stops the manager connection and rejects the pending join", async () => {
    const { client, joiner } = setup();

    const pending = joiner.join("ana");
    const failed = expect(pending).rejects.toThrow(/cancel/i);
    await vi.advanceTimersByTimeAsync(10);
    await joiner.cancel();

    await failed;
    expect(client.log).toContain("stop");
    expect(joiner.status.value).toBe("idle");
  });
});
