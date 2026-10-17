import { beforeEach, describe, expect, it, vi } from "vitest";
import fixture from "@/game/__fixtures__/gameState.json";
import { FakeHubClient } from "@/test/fakeHub";
import { useGameConnection } from "./useGameConnection";

const assignment = {
  gameServerUrl: "https://game.example.com/game",
  roomId: "55555555-5555-5555-5555-555555555555",
  nickname: "ana",
};

function setup() {
  const client = new FakeHubClient("game");
  const createClient = vi.fn(() => client);
  const connection = useGameConnection({ createClient });
  return { client, createClient, connection };
}

async function connected() {
  const context = setup();
  context.client.nextConnectionId = "connection-1";
  await context.connection.connect(assignment);
  return context;
}

describe("useGameConnection.connect", () => {
  it("uses the assigned game server url", async () => {
    const { createClient } = await connected();

    expect(createClient).toHaveBeenCalledWith(assignment.gameServerUrl);
  });

  it("registers every handler before starting", async () => {
    const { client } = await connected();
    const start = client.log.indexOf("start");

    for (const event of [
      "GameStateUpdated",
      "RoomDetailsUpdated",
      "ScoreUpdated",
      "PlayerScoreUpdated",
      "PlayerDied",
    ]) {
      expect(client.log.indexOf(`on:${event}`)).toBeGreaterThanOrEqual(0);
      expect(client.log.indexOf(`on:${event}`)).toBeLessThan(start);
    }
  });

  it("joins the room after starting", async () => {
    const { client } = await connected();

    expect(client.log.indexOf("invoke:JoinRoom")).toBeGreaterThan(client.log.indexOf("start"));
    expect(client.invocationsOf("JoinRoom")[0].args).toEqual([
      { roomId: assignment.roomId, nickname: "ana", playerTrackableId: undefined },
    ]);
  });

  it("goes connecting then connected", async () => {
    const { client, connection } = setup();
    const seen: string[] = [];
    const pending = connection.connect(assignment);
    seen.push(connection.status.value);
    await pending;
    seen.push(connection.status.value);

    expect(seen).toEqual(["connecting", "connected"]);
  });

  it("reports an error and rethrows when the hub cannot start", async () => {
    const { client, connection } = setup();
    client.startError = new Error("refused");

    await expect(connection.connect(assignment)).rejects.toThrow("refused");

    expect(connection.status.value).toBe("error");
    expect(connection.error.value).toBe("refused");
  });

  it("reports an error when JoinRoom fails", async () => {
    const { client, connection } = setup();
    client.invokeError = new Error("room full");

    await expect(connection.connect(assignment)).rejects.toThrow("room full");

    expect(connection.status.value).toBe("error");
    expect(client.log).toContain("stop");
  });
});

describe("useGameConnection server events", () => {
  it("parses GameStateUpdated into the snapshot", async () => {
    const { client, connection } = await connected();

    client.emit("GameStateUpdated", fixture);

    expect(connection.snapshot.value?.snakes[0].name).toBe("ana");
    expect(connection.room.value).toEqual({
      guid: "11111111-1111-1111-1111-111111111111",
      width: 1920,
      height: 1080,
    });
  });

  it("keeps the previous snapshot when a new one is unusable", async () => {
    const { client, connection } = await connected();
    client.emit("GameStateUpdated", fixture);
    const before = connection.snapshot.value;

    client.emit("GameStateUpdated", { garbage: true });

    expect(connection.snapshot.value).toBe(before);
  });

  it("replaces the snapshot reference on every update", async () => {
    const { client, connection } = await connected();
    client.emit("GameStateUpdated", fixture);
    const first = connection.snapshot.value;

    client.emit("GameStateUpdated", fixture);

    expect(connection.snapshot.value).not.toBe(first);
  });

  it("finds the own snake by connection id", async () => {
    const { client, connection } = await connected();

    client.emit("GameStateUpdated", fixture);

    expect(connection.self.value?.guid).toBe("22222222-2222-2222-2222-222222222222");
  });

  it("has no own snake when the connection id is unknown", async () => {
    const { client, connection } = setup();
    client.nextConnectionId = "someone-else";
    await connection.connect(assignment);

    client.emit("GameStateUpdated", fixture);

    expect(connection.self.value).toBeNull();
  });

  it("stores room details sent before the first snapshot", async () => {
    const { client, connection } = await connected();

    client.emit("RoomDetailsUpdated", { width: 800, height: 600 });

    expect(connection.room.value).toEqual({ guid: null, width: 800, height: 600 });
  });

  it("stores the leaderboard and own score", async () => {
    const { client, connection } = await connected();

    client.emit("ScoreUpdated", [{ nickname: "ana", currentScore: 9 }]);
    client.emit("PlayerScoreUpdated", 9);

    expect(connection.leaderboard.value).toEqual([{ nickname: "ana", currentScore: 9 }]);
    expect(connection.score.value).toBe(9);
  });

  it("records death with the final score", async () => {
    const { client, connection } = await connected();

    client.emit("PlayerDied", { score: 12 });

    expect(connection.death.value).toEqual({ score: 12 });
  });

  it("ignores events after dispose", async () => {
    const { client, connection } = await connected();
    await connection.dispose();

    client.emit("GameStateUpdated", fixture);

    expect(connection.snapshot.value).toBeNull();
  });
});

describe("useGameConnection controls", () => {
  it("sends a direction change", async () => {
    const { client, connection } = await connected();

    connection.changeDirection("up");

    expect(client.invocationsOf("ChangeDirection")[0].args).toEqual([{ direction: "up" }]);
  });

  it("does not resend the same direction", async () => {
    const { client, connection } = await connected();

    connection.changeDirection("up");
    connection.changeDirection("up");
    connection.changeDirection("left");
    connection.changeDirection("up");

    expect(client.invocationsOf("ChangeDirection").map((call) => (call.args[0] as any).direction)).toEqual([
      "up",
      "left",
      "up",
    ]);
  });

  it("sends shoot", async () => {
    const { client, connection } = await connected();

    connection.shoot();

    expect(client.invocationsOf("Shoot")[0].args).toEqual([{}]);
  });

  it("sends boost only when the held state changes", async () => {
    const { client, connection } = await connected();

    connection.boost(true);
    connection.boost(true);
    connection.boost(false);
    connection.boost(false);

    expect(client.invocationsOf("Boost").map((call) => call.args[0])).toEqual([{ active: true }, { active: false }]);
  });

  it("does nothing before connecting", () => {
    const { client, connection } = setup();

    connection.changeDirection("up");
    connection.shoot();
    connection.boost(true);

    expect(client.invocations).toHaveLength(0);
  });

  it("does not send controls after the player died", async () => {
    const { client, connection } = await connected();
    client.emit("PlayerDied", { score: 3 });

    connection.changeDirection("up");
    connection.shoot();

    expect(client.invocationsOf("ChangeDirection")).toHaveLength(0);
    expect(client.invocationsOf("Shoot")).toHaveLength(0);
  });

  it("swallows a failed control call and records the error", async () => {
    const { client, connection } = await connected();
    client.invokeError = new Error("hub busy");

    connection.shoot();
    await vi.waitFor(() => expect(connection.error.value).toBe("hub busy"));
  });
});

describe("useGameConnection lifecycle", () => {
  let context: Awaited<ReturnType<typeof connected>>;
  beforeEach(async () => {
    context = await connected();
    context.client.emit("GameStateUpdated", fixture);
  });

  it("marks the connection reconnecting and then connected again", () => {
    context.client.simulateReconnecting();
    expect(context.connection.status.value).toBe("reconnecting");

    context.client.simulateReconnected("connection-2");
    expect(context.connection.status.value).toBe("connected");
  });

  it("rejoins with the own player id after a reconnect", () => {
    context.client.simulateReconnected("connection-2");

    const joins = context.client.invocationsOf("JoinRoom");
    expect(joins[joins.length - 1].args).toEqual([
      {
        roomId: assignment.roomId,
        nickname: "ana",
        playerTrackableId: "22222222-2222-2222-2222-222222222222",
      },
    ]);
  });

  it("marks the connection closed when the hub closes", () => {
    context.client.simulateClose();

    expect(context.connection.status.value).toBe("closed");
  });

  it("records the reason when the hub closes with an error", () => {
    context.client.simulateClose(new Error("server gone"));

    expect(context.connection.status.value).toBe("error");
    expect(context.connection.error.value).toBe("server gone");
  });

  it("leave tells the server and stops the connection", async () => {
    await context.connection.leave();

    expect(context.client.invocationsOf("LeaveRoom")).toHaveLength(1);
    expect(context.client.log).toContain("stop");
    expect(context.connection.status.value).toBe("closed");
  });

  it("dispose removes handlers and stops", async () => {
    await context.connection.dispose();

    expect(context.client.handlerCount("GameStateUpdated")).toBe(0);
    expect(context.client.log).toContain("stop");
  });

  it("connecting again starts a fresh connection with fresh dedupe memory", async () => {
    const first = new FakeHubClient("game");
    const second = new FakeHubClient("game");
    const clients = [first, second];
    const connection = useGameConnection({ createClient: () => clients.shift()! });
    await connection.connect(assignment);
    connection.changeDirection("up");

    await connection.connect(assignment);
    connection.changeDirection("up");

    expect(first.log).toContain("stop");
    expect(second.invocationsOf("ChangeDirection")).toHaveLength(1);
  });
});
