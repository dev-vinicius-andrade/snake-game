import { computed, getCurrentScope, onScopeDispose, shallowRef } from "vue";
import { parseUnlockedIds } from "@/game/achievements";
import { parseDamage } from "@/game/damage";
import { recentDamage } from "@/render/damageNumbers";
import { parseLeaderboard, parseRoomSize, parseSnapshot } from "@/game/snapshot";
import { angleDistance } from "@/input/angles";
import { createHubClient, type HubClient, type HubClientFactory } from "@/services/hub";
import type {
  ControlDirection,
  GameConnectionStatus,
  FloatingDamage,
  GameSnapshot,
  JoinAssignment,
  LeaderboardEntry,
  RoomSnapshot,
} from "@/types/game";

export interface UseGameConnectionOptions {
  createClient?: HubClientFactory;
  /** How often the round trip to the server is timed. Zero (the default) does not measure it. */
  pingIntervalMs?: number;
  /** Clock in milliseconds. Defaults to `performance.now`. */
  now?: () => number;
}

/** A new measurement counts this much against the earlier ones, so one slow answer does not make the number jump. */
const PING_SMOOTHING = 0.3;

type Handler = (...args: any[]) => void;

/** At most about thirty steering updates per second, and none for changes under two degrees. */
const STEER_MIN_INTERVAL_MS = 33;
const STEER_MIN_DELTA_RADIANS = (2 * Math.PI) / 180;

const messageOf = (failure: unknown) => (failure instanceof Error ? failure.message : String(failure));

/**
 * One game hub session: joins a room, exposes the latest server state as `shallowRef`s
 * (replaced on every update, never deeply proxied) and sends player controls.
 */
export function useGameConnection(options: UseGameConnectionOptions = {}) {
  const createClient: HubClientFactory = options.createClient ?? createHubClient;
  const pingIntervalMs = options.pingIntervalMs ?? 0;
  const now = options.now ?? (() => performance.now());

  const status = shallowRef<GameConnectionStatus>("idle");
  const error = shallowRef<string | null>(null);
  const snapshot = shallowRef<GameSnapshot | null>(null);
  const room = shallowRef<RoomSnapshot | null>(null);
  const leaderboard = shallowRef<LeaderboardEntry[]>([]);
  const score = shallowRef(0);
  const death = shallowRef<{ score: number } | null>(null);
  /** An administrator closed the room this session was playing in. */
  const roomClosed = shallowRef(false);
  const connectionId = shallowRef<string | null>(null);
  /** Round trip to the game server in milliseconds, smoothed; null until measured. */
  const ping = shallowRef<number | null>(null);
  let pingTimer: ReturnType<typeof setInterval> | null = null;
  /** Damage numbers still on their way up the screen, oldest first. */
  const damage = shallowRef<FloatingDamage[]>([]);
  /** Achievements the server unlocked for this player, one entry per event; a view shows a toast for each and does not need to clear them. */
  const unlocked = shallowRef<Array<{ seq: number; ids: string[] }>>([]);
  let unlockedSeq = 0;

  let client: HubClient | null = null;
  let session: JoinAssignment | null = null;
  let handlers: Record<string, Handler> = {};
  let lastDirection: ControlDirection | null = null;
  let lastBoost = false;
  let lastSteer: number | null = null;
  let lastSteerAt = Number.NEGATIVE_INFINITY;
  let pendingSteer: number | null = null;
  let steerTimer: ReturnType<typeof setTimeout> | null = null;

  const self = computed(
    () => snapshot.value?.snakes.find((snake) => snake.connectionId === connectionId.value) ?? null
  );

  function buildHandlers(): Record<string, Handler> {
    // The server also sends these, but the snapshot carries the same information. Listening keeps SignalR from warning.
    const ignored = () => undefined;
    return {
      PlayerJoined: ignored,
      PlayerLefted: ignored,
      ConnectedPlayersUpdated: ignored,
      FoodGenerated: ignored,
      Snapshot: (raw: unknown) => {
        const next = parseSnapshot(raw);
        if (!next) return;
        snapshot.value = next;
        room.value = next.room;
      },
      RoomDetailsUpdated: (raw: unknown) => {
        const size = parseRoomSize(raw);
        if (size) room.value = { guid: room.value?.guid ?? null, ...size };
      },
      ScoreUpdated: (raw: unknown) => {
        leaderboard.value = parseLeaderboard(raw);
      },
      PlayerScoreUpdated: (value: unknown) => {
        if (typeof value === "number") score.value = value;
      },
      RoomClosed: () => {
        roomClosed.value = true;
      },
      DamageDealt: (raw: unknown) => {
        const event = parseDamage(raw);
        if (event) {
          const now = performance.now();
          damage.value = [...recentDamage(damage.value, now), { ...event, bornMs: now }];
        }
      },
      AchievementsUnlocked: (raw: unknown) => {
        const ids = parseUnlockedIds(raw);
        if (ids.length > 0) unlocked.value = [...unlocked.value, { seq: ++unlockedSeq, ids }];
      },
      PlayerDied: (raw: { score?: number } | null) => {
        death.value = { score: typeof raw?.score === "number" ? raw.score : score.value };
      },
    };
  }

  function resetState() {
    snapshot.value = null;
    room.value = null;
    leaderboard.value = [];
    score.value = 0;
    death.value = null;
    roomClosed.value = false;
    damage.value = [];
    unlocked.value = [];
    ping.value = null;
    error.value = null;
    connectionId.value = null;
    lastDirection = null;
    lastBoost = false;
    cancelPendingSteer();
    lastSteer = null;
    lastSteerAt = Number.NEGATIVE_INFINITY;
  }

  function cancelPendingSteer() {
    if (steerTimer !== null) clearTimeout(steerTimer);
    steerTimer = null;
    pendingSteer = null;
  }

  async function detach() {
    cancelPendingSteer();
    stopPing();
    const previous = client;
    if (!previous) return;
    client = null;
    Object.entries(handlers).forEach(([event, handler]) => previous.off(event, handler));
    handlers = {};
    await previous.stop().catch(() => undefined);
  }

  function stopPing() {
    if (pingTimer !== null) clearInterval(pingTimer);
    pingTimer = null;
  }

  /** Times an empty call to the server now and every interval, for as long as this client is the current one. */
  function startPing(current: HubClient) {
    stopPing();
    if (pingIntervalMs <= 0) return;
    const measure = async () => {
      if (client !== current || current.state !== "connected") return;
      const started = now();
      try {
        await current.invoke("Ping");
      } catch {
        return;
      }
      if (client !== current) return;
      const sample = now() - started;
      ping.value = ping.value === null ? Math.round(sample) : Math.round(ping.value * (1 - PING_SMOOTHING) + sample * PING_SMOOTHING);
    };
    void measure();
    pingTimer = setInterval(measure, pingIntervalMs);
  }

  function watchLifecycle(current: HubClient) {
    const isCurrent = () => client === current;
    current.onClose((failure) => {
      if (!isCurrent()) return;
      status.value = failure ? "error" : "closed";
      error.value = failure ? failure.message : null;
    });
    current.onReconnecting(() => {
      if (isCurrent()) status.value = "reconnecting";
    });
    current.onReconnected((newConnectionId) => {
      if (!isCurrent()) return;
      const knownPlayerGuid = self.value?.guid;
      connectionId.value = newConnectionId ?? current.connectionId;
      lastDirection = null;
      lastBoost = false;
      status.value = "connected";
      send("JoinRoom", joinRequest(knownPlayerGuid));
    });
  }

  function joinRequest(playerTrackableId?: string) {
    return { roomId: session!.roomId, nickname: session!.nickname, playerTrackableId };
  }

  async function connect(assignment: JoinAssignment) {
    status.value = "connecting";
    await detach();
    resetState();
    session = assignment;

    const current = createClient(assignment.gameServerUrl);
    client = current;
    handlers = buildHandlers();
    Object.entries(handlers).forEach(([event, handler]) => current.on(event, handler));
    watchLifecycle(current);
    try {
      await current.start();
      connectionId.value = current.connectionId;
      await current.invoke("JoinRoom", joinRequest());
      status.value = "connected";
      startPing(current);
    } catch (failure) {
      status.value = "error";
      error.value = messageOf(failure);
      await detach();
      throw failure;
    }
  }

  const canSend = () => client !== null && status.value === "connected" && death.value === null;

  function send(method: string, payload: unknown) {
    client?.invoke(method, payload).catch((failure) => {
      error.value = messageOf(failure);
    });
  }

  function changeDirection(direction: ControlDirection) {
    if (!canSend() || direction === lastDirection) return;
    lastDirection = direction;
    // A keyboard turn moves the server heading, so the last steering angle no longer describes it.
    lastSteer = null;
    cancelPendingSteer();
    send("ChangeDirection", { direction });
  }

  /** Heading in radians (0 is right, positive turns clockwise on screen). Throttled, and tiny changes are dropped. */
  function steer(angle: number) {
    if (!Number.isFinite(angle) || !canSend()) return;
    if (lastSteer !== null && angleDistance(angle, lastSteer) < STEER_MIN_DELTA_RADIANS) {
      pendingSteer = null;
      return;
    }
    const wait = lastSteerAt + STEER_MIN_INTERVAL_MS - Date.now();
    if (wait <= 0) {
      sendSteer(angle);
      return;
    }
    pendingSteer = angle;
    steerTimer ??= setTimeout(flushSteer, wait);
  }

  function sendSteer(angle: number) {
    lastSteer = angle;
    lastSteerAt = Date.now();
    pendingSteer = null;
    send("Steer", { angle });
  }

  function flushSteer() {
    steerTimer = null;
    const angle = pendingSteer;
    pendingSteer = null;
    if (angle !== null) steer(angle);
  }

  function shoot() {
    if (canSend()) send("Shoot", {});
  }

  function boost(active: boolean) {
    if (!canSend() || active === lastBoost) return;
    lastBoost = active;
    send("Boost", { active });
  }

  async function leave() {
    if (!client) return;
    await client.invoke("LeaveRoom", {}).catch(() => undefined);
    await detach();
    status.value = "closed";
  }

  async function dispose() {
    await detach();
  }

  if (getCurrentScope()) onScopeDispose(() => void dispose());

  return {
    status,
    error,
    snapshot,
    room,
    leaderboard,
    score,
    death,
    damage,
    unlocked,
    ping,
    roomClosed,
    connectionId,
    self,
    connect,
    changeDirection,
    steer,
    shoot,
    boost,
    leave,
    dispose,
  };
}
