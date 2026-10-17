import { describe, expect, it, vi } from "vitest";
import { adaptConnection, type SignalRConnectionLike } from "./hub";

function fakeConnection(overrides: Partial<SignalRConnectionLike> = {}) {
  const connection = {
    connectionId: null as string | null,
    state: "Disconnected",
    start: vi.fn(async function (this: unknown) {
      return;
    }),
    stop: vi.fn(async () => undefined),
    on: vi.fn(),
    off: vi.fn(),
    invoke: vi.fn(async () => "result"),
    onclose: vi.fn(),
    onreconnecting: vi.fn(),
    onreconnected: vi.fn(),
    ...overrides,
  };
  return connection as unknown as SignalRConnectionLike & typeof connection;
}

describe("adaptConnection", () => {
  it("reads connectionId lazily so a later connection id is visible", () => {
    const connection = fakeConnection();
    const client = adaptConnection(connection);

    expect(client.connectionId).toBeNull();
    (connection as { connectionId: string | null }).connectionId = "abc";

    expect(client.connectionId).toBe("abc");
  });

  it.each([
    ["Disconnected", "disconnected"],
    ["Connecting", "connecting"],
    ["Connected", "connected"],
    ["Reconnecting", "reconnecting"],
    ["Disconnecting", "disconnecting"],
  ])("maps SignalR state %s to %s", (signalRState, expected) => {
    const connection = fakeConnection({ state: signalRState } as Partial<SignalRConnectionLike>);

    expect(adaptConnection(connection).state).toBe(expected);
  });

  it("reads state lazily", () => {
    const connection = fakeConnection();
    const client = adaptConnection(connection);

    (connection as { state: string }).state = "Connected";

    expect(client.state).toBe("connected");
  });

  it("keeps the connection as this when calling its methods", async () => {
    let receiver: unknown;
    const connection = fakeConnection({
      start: async function (this: unknown) {
        receiver = this;
      },
    });

    await adaptConnection(connection).start();

    expect(receiver).toBe(connection);
  });

  it("forwards invoke arguments and result", async () => {
    const connection = fakeConnection();

    const result = await adaptConnection(connection).invoke("Shoot", { a: 1 });

    expect(connection.invoke).toHaveBeenCalledWith("Shoot", { a: 1 });
    expect(result).toBe("result");
  });

  it("registers and removes event handlers", () => {
    const connection = fakeConnection();
    const client = adaptConnection(connection);
    const handler = () => undefined;

    client.on("GameStateUpdated", handler);
    client.off("GameStateUpdated", handler);

    expect(connection.on).toHaveBeenCalledWith("GameStateUpdated", handler);
    expect(connection.off).toHaveBeenCalledWith("GameStateUpdated", handler);
  });

  it("forwards lifecycle callbacks", () => {
    const connection = fakeConnection();
    const client = adaptConnection(connection);
    const handler = () => undefined;

    client.onClose(handler);
    client.onReconnecting(handler);
    client.onReconnected(handler);

    expect(connection.onclose).toHaveBeenCalledWith(handler);
    expect(connection.onreconnecting).toHaveBeenCalledWith(handler);
    expect(connection.onreconnected).toHaveBeenCalledWith(handler);
  });
});
