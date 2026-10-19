import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { FakeHubClient } from "@/test/fakeHub";
import { useRoomCreation } from "./useRoomCreation";

const created = { roomId: "55555555-5555-5555-5555-555555555555", code: "ABC234", name: "Friends", maxPlayers: 4 };

function setup(options: { postCreate?: (url: string, body: unknown) => Promise<void>; timeoutMs?: number } = {}) {
  const client = new FakeHubClient("manager");
  const postCreate = vi.fn(options.postCreate ?? (async () => undefined));
  const createClient = vi.fn(() => client);
  const creation = useRoomCreation({ managerUrl: "http://manager.test/", createClient, postCreate, timeoutMs: options.timeoutMs });
  return { client, postCreate, createClient, creation };
}

describe("useRoomCreation", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it("posts the room with the hub connection id and resolves with the created room", async () => {
    const { client, postCreate, createClient, creation } = setup({ postCreate: async () => client.emit("onRoomCreated", created) });

    const room = await creation.create({ name: "Friends", maxPlayers: 4 });

    expect(createClient).toHaveBeenCalledWith("http://manager.test/manager");
    expect(postCreate).toHaveBeenCalledWith("http://manager.test/rooms", { id: "conn-1", name: "Friends", maxPlayers: 4 });
    expect(room).toEqual(created);
  });

  it("listens before the connection starts, so an early answer is not missed", async () => {
    const { client, creation } = setup({ postCreate: async () => client.emit("onRoomCreated", created) });

    await creation.create({ name: "Friends", maxPlayers: 4 });

    expect(client.log.indexOf("on:onRoomCreated")).toBeLessThan(client.log.indexOf("start"));
  });

  it("closes the manager connection afterwards", async () => {
    const { client, creation } = setup({ postCreate: async () => client.emit("onRoomCreated", created) });

    await creation.create({ name: "Friends", maxPlayers: 4 });

    expect(client.log).toContain("stop");
    expect(client.handlerCount("onRoomCreated")).toBe(0);
    expect(client.handlerCount("onRoomCreateFailed")).toBe(0);
  });

  it("reports progress through status", async () => {
    const { client, creation } = setup({ postCreate: async () => client.emit("onRoomCreated", created) });
    expect(creation.status.value).toBe("idle");

    await creation.create({ name: "Friends", maxPlayers: 4 });

    expect(creation.status.value).toBe("created");
  });

  it("fails with a readable message when the server refuses", async () => {
    const { client, creation } = setup({ postCreate: async () => client.emit("onRoomCreateFailed", { reason: "codeTaken" }) });

    await expect(creation.create({ name: "Friends", maxPlayers: 4 })).rejects.toThrow(/try again/i);
    expect(creation.status.value).toBe("error");
    expect(creation.error.value).toMatch(/try again/i);
  });

  it("times out when no game server makes the room", async () => {
    const { creation } = setup({ timeoutMs: 1000 });

    const pending = creation.create({ name: "Friends", maxPlayers: 4 });
    const failed = expect(pending).rejects.toThrow(/timed out/i);
    await vi.advanceTimersByTimeAsync(1001);

    await failed;
    expect(creation.status.value).toBe("error");
  });

  it("fails when the post fails", async () => {
    const { creation } = setup({
      postCreate: async () => {
        throw new Error("500");
      },
    });

    await expect(creation.create({ name: "Friends", maxPlayers: 4 })).rejects.toThrow("500");
  });

  it.each([
    [{ roomId: "", code: "ABC234" }],
    [{ roomId: created.roomId }],
    [null],
  ])("rejects an invalid answer %j", async (payload) => {
    const { client, creation } = setup({ postCreate: async () => client.emit("onRoomCreated", payload) });

    await expect(creation.create({ name: "Friends", maxPlayers: 4 })).rejects.toThrow(/invalid/i);
  });

  it("trims the name and sends no name when it is blank", async () => {
    const { client, postCreate, creation } = setup({ postCreate: async () => client.emit("onRoomCreated", created) });

    await creation.create({ name: "   ", maxPlayers: 4 });

    expect(postCreate).toHaveBeenCalledWith(expect.any(String), { id: "conn-1", name: undefined, maxPlayers: 4 });
  });

  it("cancel rejects the pending creation", async () => {
    const { creation } = setup();

    const pending = creation.create({ name: "Friends", maxPlayers: 4 });
    const failed = expect(pending).rejects.toThrow(/cancel/i);
    await vi.advanceTimersByTimeAsync(10);
    await creation.cancel();

    await failed;
    expect(creation.status.value).toBe("idle");
  });

  it("asks for the gameplay the host picked, and only what differs from the default", async () => {
    const holder: { client?: { emit: (name: string, payload: unknown) => void } } = {};
    const { client, postCreate, creation } = setup({ postCreate: async () => holder.client!.emit("onRoomCreated", { roomId: "r", code: "ABC234" }) });
    holder.client = client;

    await creation.create({ name: "", maxPlayers: 0, gameplay: { turret: "off", lightning: "wild" } });

    expect(postCreate).toHaveBeenCalledWith("http://manager.test/rooms", { id: "conn-1", name: undefined, maxPlayers: 0, gameplay: { turret: "off", lightning: "wild" } });
  });

  it("leaves the gameplay out when the host picked nothing", async () => {
    const holder: { client?: { emit: (name: string, payload: unknown) => void } } = {};
    const { client, postCreate, creation } = setup({ postCreate: async () => holder.client!.emit("onRoomCreated", { roomId: "r", code: "ABC234" }) });
    holder.client = client;

    await creation.create({ name: "", maxPlayers: 0, gameplay: {} });

    expect(Object.keys(postCreate.mock.calls[0][1] as object)).not.toContain("gameplay");
  });
});
