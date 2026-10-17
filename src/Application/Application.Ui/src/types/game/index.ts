/**
 * Client side model of what the game server sends. The wire shape is pinned by
 * SnapshotContractTests on the server; `game/snapshot.ts` turns the wire shape into these types.
 */
export type ControlDirection = "left" | "up" | "right" | "down";
export type SnakeDirection = ControlDirection | "angular";

export interface GamePoint {
  x: number;
  y: number;
}

export interface GameColor {
  fillColor: string;
  border: string;
}

export interface SnakeSnapshot {
  guid: string;
  /** SignalR connection id of the owner; used to find the own snake. */
  connectionId: string;
  name: string;
  /** Last element of `path`. */
  head: GamePoint;
  /** One point per body segment, ordered tail to head. */
  path: GamePoint[];
  /** HP ratio 0..1 per segment, aligned with `path`. */
  segmentHealth: number[];
  isAlive: boolean;
  isBoosting: boolean;
  score: number;
  color: GameColor;
  direction: SnakeDirection;
}

export interface FoodSnapshot {
  guid: string;
  position: GamePoint;
  color: GameColor;
  value: number;
}

export interface ShotSnapshot {
  guid: string;
  position: GamePoint;
  size: number;
  ownerGuid?: string;
}

export interface RoomDimensions {
  width: number;
  height: number;
}

export interface RoomSnapshot extends RoomDimensions {
  guid: string | null;
}

export interface GameSnapshot {
  room: RoomSnapshot;
  snakes: SnakeSnapshot[];
  foods: FoodSnapshot[];
  shots: ShotSnapshot[];
}

export interface LeaderboardEntry {
  nickname: string;
  currentScore: number;
}

/** What the manager api tells the browser once a game server accepted the player. */
export interface JoinAssignment {
  /** Full hub url of the game server, already ending in the hub path. */
  gameServerUrl: string;
  roomId: string;
  nickname: string;
}

export type GameConnectionStatus = "idle" | "connecting" | "connected" | "reconnecting" | "closed" | "error";
