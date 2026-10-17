import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { FakeHubClient } from "@/test/fakeHub";
import { useManagerJoin } from "./useManagerJoin";

const assignment = {
  gameServerUrl: "https://game.example.com/game",
  roomId: "55555555-5555-5555-5555-555555555555",
  nickname: "ana",
};

function setup(postJoin: (client: FakeHubClient) => Promise<void> = async () => undefined) {
  const client = new FakeHubClient("manager");
  const post = vi.fn(() => postJoin(client));
  const joiner = useManagerJoin({ managerUrl: "http://manager.test", createClient: () => client, postJoin: post });
  return { client, post, joiner };
}

describe("useManagerJoin by invite code and failures", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });
  afterEach(() => {
    vi.useRealTimers();
  });

  it("joins through the invite code route", async () => {
    const { post, joiner } = setup(async (client) => client.emit("onJoinedRoom", assignment));

    await joiner.join("ana", { code: "ABC234" });

    expect(post).toHaveBeenCalledWith("http://manager.test/join/code/ABC234", { id: "conn-1", nickname: "ana" });
  });

  it("still joins a room by its id when given a string", async () => {
    const { post, joiner } = setup(async (client) => client.emit("onJoinedRoom", assignment));

    await joiner.join("ana", assignment.roomId);

    expect(post).toHaveBeenCalledWith(`http://manager.test/join/room/${assignment.roomId}`, expect.anything());
  });

  it.each([
    ["roomFull", /full/i],
    ["noCapacity", /free/i],
    ["something else", /could not join/i],
  ])("fails with a readable message when the game server says %s", async (reason, message) => {
    const { joiner } = setup(async (client) => client.emit("onJoinFailed", { reason }));

    await expect(joiner.join("ana")).rejects.toThrow(message);
    expect(joiner.status.value).toBe("error");
  });

  it("stops listening for failures once done", async () => {
    const { client, joiner } = setup(async (c) => c.emit("onJoinedRoom", assignment));

    await joiner.join("ana");

    expect(client.handlerCount("onJoinFailed")).toBe(0);
  });
});
