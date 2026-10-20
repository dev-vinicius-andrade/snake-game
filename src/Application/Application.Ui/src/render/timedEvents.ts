import type { GameSnapshot } from "@/types/game";

/** The events that are only a countdown for the player: a fruit shower (fruit falls all over the room) and a sugar rush (boost is free). */
export type TimedEventKind = "shower" | "rush";

export interface TimedEvent {
  kind: TimedEventKind;
  /** Milliseconds left, as of the snapshot. */
  msLeft: number;
}

/** The ones going now, the shower first. Both can go at once. */
export function timedEvents(snapshot: GameSnapshot): TimedEvent[] {
  const going: TimedEvent[] = [];
  if ((snapshot.showerMsLeft ?? 0) > 0) going.push({ kind: "shower", msLeft: snapshot.showerMsLeft ?? 0 });
  if ((snapshot.rushMsLeft ?? 0) > 0) going.push({ kind: "rush", msLeft: snapshot.rushMsLeft ?? 0 });
  return going;
}
