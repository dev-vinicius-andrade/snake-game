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
  /** Ids of the perks the snake has earned, in the order it earned them. */
  perks: string[];
  /** True with the auto fire perk: holding the shoot control keeps shooting. */
  autoFire: boolean;
  /** Foods eaten in this life; perks are earned from it. */
  foodsEaten: number;
  score: number;
  color: GameColor;
  direction: SnakeDirection;
}

export type FoodKind = "normal" | "prize";

export interface FoodSnapshot {
  guid: string;
  /** The prize is rare and valuable; snakes fight over it. */
  kind: FoodKind;
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

/** What is coming down on a spot of the map. */
export type StrikeKind = "lightning" | "meteor";

/** A spot lightning or a meteor is about to strike. `msLeft` is the time left when the snapshot was sent. */
export interface StrikeSnapshot {
  guid: string;
  kind: StrikeKind;
  position: GamePoint;
  /** World units around the spot that the strike reaches. */
  radius: number;
  msLeft: number;
}

export type DamageKind = "hit" | "broke" | "kill";

/** A shot did damage: sent to the shooter and to the snake that was hit. */
export interface DamageEvent {
  shooterGuid: string;
  targetGuid: string;
  /** World position of the segment that was hit. */
  x: number;
  y: number;
  /** HP taken from the segment; 0 for a kill. */
  damage: number;
  kind: DamageKind;
}

/** A damage event on its way up the screen. `bornMs` is the clock reading (performance.now) when it arrived. */
export interface FloatingDamage extends DamageEvent {
  bornMs: number;
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
  /** Lightning warnings. Empty when the server sends none. */
  strikes: StrikeSnapshot[];
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
