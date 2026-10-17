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

/** A room by id, or by the invite code its host shared. */
export type JoinTarget = string | { code: string };

const defaultPostJoin: PostJoin = async (url, body) => {
  try {
    await axios.post(url, body);
  } catch (failure) {
    if (axios.isAxiosError(failure) && failure.response?.status === 404) throw new Error("That room does not exist anymore");
    throw failure;
  }
};

const JOIN_FAILURES: Record<string, string> = {
  roomFull: "That room is full",
  noCapacity: "No game server is free right now",
};

function joinFailureMessage(payload: unknown): string {
  const reason = (payload as { reason?: unknown } | null)?.reason;
  return (typeof reason === "string" && JOIN_FAILURES[reason]) || "Could not join the game";
}

function joinRoute(target?: JoinTarget): string {
  if (!target) return "join";
  return typeof target === "string" ? `join/room/${target}` : `join/code/${encodeURIComponent(target.code)}`;
}

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

/** Listens for the manager event `onJoinedRoom` (sent by PlayerController.Joined) and times out. Created before the hub starts so an early assignment is never missed. */
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
  const failureHandler = (payload: unknown) => settle.reject(new Error(joinFailureMessage(payload)));
  client.on("onJoinedRoom", handler);
  client.on("onJoinFailed", failureHandler);

  return {
    promise,
    reject: (reason) => settle.reject(reason),
    dispose: () => {
      clearTimeout(timer);
      client.off("onJoinedRoom", handler);
      client.off("onJoinFailed", failureHandler);
    },
  };
}

/**
 * Asks the manager api for a game server. The manager answers asynchronously over its hub:
 * connect to the hub, post the join with the hub connection id, wait for `onJoinedRoom`.
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

  async function join(nickname: string, target?: JoinTarget): Promise<JoinAssignment> {
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
      return await requestAssignment(client, waiter, trimmed, target);
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

  async function requestAssignment(client: HubClient, waiter: AssignmentWaiter, nickname: string, target?: JoinTarget) {
    await client.start();
    if (!client.connectionId) throw new Error("The manager hub did not provide a connection id");
    status.value = "waiting";
    await postJoin(`${managerUrl}/${joinRoute(target)}`, { id: client.connectionId, nickname });
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
