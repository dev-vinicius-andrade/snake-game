import axios from "axios";
import { shallowRef } from "vue";
import { resolveManagerUrl } from "@/config/env";
import { createHubClient, type HubClient, type HubClientFactory } from "@/services/hub";

export type RoomCreationStatus = "idle" | "connecting" | "waiting" | "created" | "error";

export interface NewRoom {
  /** Empty or blank means the server picks a name. */
  name: string;
  /** Zero means the server default. */
  maxPlayers: number;
  /** What the room does differently from the default gameplay: feature to preset. Only what differs is sent. */
  gameplay?: Record<string, string>;
}

export interface CreatedRoom {
  roomId: string;
  code: string;
  name: string | null;
  maxPlayers: number;
}

export type PostCreate = (
  url: string,
  body: { id: string; name: string | undefined; maxPlayers: number; gameplay?: Record<string, string> }
) => Promise<void>;

export interface UseRoomCreationOptions {
  managerUrl?: string;
  createClient?: HubClientFactory;
  postCreate?: PostCreate;
  timeoutMs?: number;
}

const DEFAULT_TIMEOUT_MS = 15_000;

const defaultPostCreate: PostCreate = async (url, body) => {
  await axios.post(url, body);
};

const FAILURES: Record<string, string> = {
  codeTaken: "That invite code was taken, please try again",
};

function parseCreated(raw: unknown): CreatedRoom {
  const value = (typeof raw === "object" && raw !== null ? raw : {}) as Record<string, unknown>;
  const { roomId, code, name, maxPlayers } = value;
  if (typeof roomId !== "string" || !roomId || typeof code !== "string" || !code) {
    throw new Error("Invalid room received from the manager");
  }
  return { roomId, code, name: typeof name === "string" ? name : null, maxPlayers: typeof maxPlayers === "number" ? maxPlayers : 0 };
}

function failureMessage(payload: unknown): string {
  const reason = (payload as { reason?: unknown } | null)?.reason;
  return (typeof reason === "string" && FAILURES[reason]) || "The room could not be created, please try again";
}

interface Waiter {
  promise: Promise<CreatedRoom>;
  reject(reason: Error): void;
  dispose(): void;
}

/** Listens for `onRoomCreated` and `onRoomCreateFailed`. Created before the hub starts so an early answer is never missed. */
function waitForRoom(client: HubClient, timeoutMs: number): Waiter {
  let settle: { resolve(value: CreatedRoom): void; reject(reason: Error): void };
  const promise = new Promise<CreatedRoom>((resolve, reject) => (settle = { resolve, reject }));
  promise.catch(() => undefined);

  const created = (payload: unknown) => {
    try {
      settle.resolve(parseCreated(payload));
    } catch (error) {
      settle.reject(error as Error);
    }
  };
  const failed = (payload: unknown) => settle.reject(new Error(failureMessage(payload)));
  const timer = setTimeout(() => settle.reject(new Error("Timed out waiting for a game server to create the room")), timeoutMs);
  client.on("onRoomCreated", created);
  client.on("onRoomCreateFailed", failed);

  return {
    promise,
    reject: (reason) => settle.reject(reason),
    dispose: () => {
      clearTimeout(timer);
      client.off("onRoomCreated", created);
      client.off("onRoomCreateFailed", failed);
    },
  };
}

/** Asks the manager for a private room. Like joining, the answer comes over the manager hub. */
export function useRoomCreation(options: UseRoomCreationOptions = {}) {
  const managerUrl = (options.managerUrl ?? resolveManagerUrl()).replace(/\/+$/, "");
  const createClient = options.createClient ?? createHubClient;
  const postCreate = options.postCreate ?? defaultPostCreate;
  const timeoutMs = options.timeoutMs ?? DEFAULT_TIMEOUT_MS;

  const status = shallowRef<RoomCreationStatus>("idle");
  const error = shallowRef<string | null>(null);
  let active: { client: HubClient; waiter: Waiter } | null = null;
  let cancelled = false;

  async function create(newRoom: NewRoom): Promise<CreatedRoom> {
    await cancel();
    cancelled = false;
    error.value = null;
    status.value = "connecting";
    const client = createClient(`${managerUrl}/manager`);
    const waiter = waitForRoom(client, timeoutMs);
    active = { client, waiter };
    try {
      return await request(client, waiter, newRoom);
    } catch (failure) {
      status.value = cancelled ? "idle" : "error";
      error.value = cancelled ? null : (failure as Error).message;
      throw failure;
    } finally {
      waiter.dispose();
      active = null;
      await client.stop().catch(() => undefined);
    }
  }

  async function request(client: HubClient, waiter: Waiter, newRoom: NewRoom) {
    await client.start();
    if (!client.connectionId) throw new Error("The manager hub did not provide a connection id");
    status.value = "waiting";
    const name = newRoom.name.trim();
    const gameplay = newRoom.gameplay && Object.keys(newRoom.gameplay).length > 0 ? { gameplay: newRoom.gameplay } : {};
    await postCreate(`${managerUrl}/rooms`, { id: client.connectionId, name: name || undefined, maxPlayers: newRoom.maxPlayers, ...gameplay });
    const created = await waiter.promise;
    status.value = "created";
    return created;
  }

  async function cancel() {
    if (!active) return;
    cancelled = true;
    active.waiter.reject(new Error("Room creation cancelled"));
    await active.client.stop().catch(() => undefined);
  }

  return { status, error, create, cancel };
}
