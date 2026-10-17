import axios from "axios";
import { shallowRef } from "vue";
import { resolveManagerUrl } from "@/config/env";
import { createHubClient, type HubClient, type HubClientFactory } from "@/services/hub";
import type { JoinAssignment } from "@/types/game";

export type ManagerJoinStatus = "idle" | "connecting" | "waiting" | "assigned" | "error";
export type PostJoin = (url: string, body: { id: string; nickname: string }) => Promise<void>;

export interface UseManagerJoinOptions {
  managerUrl?: string;
  createClient?: HubClientFactory;
  postJoin?: PostJoin;
  /** How long to wait for a game server to accept the player. */
  timeoutMs?: number;
}

const DEFAULT_TIMEOUT_MS = 15_000;

const defaultPostJoin: PostJoin = async (url, body) => {
  await axios.post(url, body);
};

function parseAssignment(raw: unknown): JoinAssignment {
  const value = (typeof raw === "object" && raw !== null ? raw : {}) as Record<string, unknown>;
  const { gameServerUrl, roomId, nickname } = value;
  if (typeof gameServerUrl !== "string" || !gameServerUrl || typeof roomId !== "string" || !roomId) {
    throw new Error("Invalid assignment received from the manager");
  }
  return { gameServerUrl, roomId, nickname: typeof nickname === "string" ? nickname : "" };
}

interface AssignmentWaiter {
  promise: Promise<JoinAssignment>;
  reject(reason: Error): void;
  dispose(): void;
}

/** Listens for `onJoin` and times out. Created before the hub starts so an early assignment is never missed. */
function waitForAssignment(client: HubClient, timeoutMs: number): AssignmentWaiter {
  let settle: { resolve(value: JoinAssignment): void; reject(reason: Error): void };
  const promise = new Promise<JoinAssignment>((resolve, reject) => (settle = { resolve, reject }));
  promise.catch(() => undefined);

  const handler = (payload: unknown) => {
    try {
      settle.resolve(parseAssignment(payload));
    } catch (error) {
      settle.reject(error as Error);
    }
  };
  const timer = setTimeout(
    () => settle.reject(new Error("Timed out waiting for a game server to accept the player")),
    timeoutMs
  );
  client.on("onJoin", handler);

  return {
    promise,
    reject: (reason) => settle.reject(reason),
    dispose: () => {
      clearTimeout(timer);
      client.off("onJoin", handler);
    },
  };
}

/**
 * Asks the manager api for a game server. The manager answers asynchronously over its hub:
 * connect to the hub, post the join with the hub connection id, wait for `onJoin`.
 */
export function useManagerJoin(options: UseManagerJoinOptions = {}) {
  const managerUrl = (options.managerUrl ?? resolveManagerUrl()).replace(/\/+$/, "");
  const createClient = options.createClient ?? createHubClient;
  const postJoin = options.postJoin ?? defaultPostJoin;
  const timeoutMs = options.timeoutMs ?? DEFAULT_TIMEOUT_MS;

  const status = shallowRef<ManagerJoinStatus>("idle");
  const error = shallowRef<string | null>(null);
  let active: { client: HubClient; waiter: AssignmentWaiter } | null = null;
  let cancelled = false;

  async function join(nickname: string, roomId?: string): Promise<JoinAssignment> {
    const trimmed = nickname.trim();
    if (!trimmed) throw new Error("A nickname is required to join");
    await cancel();

    cancelled = false;
    error.value = null;
    status.value = "connecting";
    const client = createClient(`${managerUrl}/manager`);
    const waiter = waitForAssignment(client, timeoutMs);
    active = { client, waiter };
    try {
      return await requestAssignment(client, waiter, trimmed, roomId);
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

  async function requestAssignment(client: HubClient, waiter: AssignmentWaiter, nickname: string, roomId?: string) {
    await client.start();
    if (!client.connectionId) throw new Error("The manager hub did not provide a connection id");
    status.value = "waiting";
    const route = roomId ? `join/room/${roomId}` : "join";
    await postJoin(`${managerUrl}/${route}`, { id: client.connectionId, nickname });
    const assignment = await waiter.promise;
    status.value = "assigned";
    return assignment;
  }

  async function cancel() {
    if (!active) return;
    cancelled = true;
    active.waiter.reject(new Error("Join cancelled"));
    await active.client.stop().catch(() => undefined);
  }

  return { status, error, join, cancel };
}
