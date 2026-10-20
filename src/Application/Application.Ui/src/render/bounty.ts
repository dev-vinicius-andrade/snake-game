import type { GameSnapshot, SnakeSnapshot } from "@/types/game";

/** The living snake there is a bounty on, or null. */
export function wantedSnake(snapshot: GameSnapshot): SnakeSnapshot | null {
  const bounty = snapshot.bounty;
  if (!bounty) return null;
  return snapshot.snakes.find((snake) => snake.guid === bounty.guid && snake.isAlive) ?? null;
}

/** What the player is told about a bounty: on whom, the reward, the time left, and whether it is on the own snake. */
export interface BountyWarning {
  name: string;
  msLeft: number;
  rewardMass: number;
  self: boolean;
}

/** Null while there is no bounty on a living snake. */
export function bountyWarning(snapshot: GameSnapshot, selfGuid: string | null | undefined): BountyWarning | null {
  const wanted = wantedSnake(snapshot);
  if (!wanted || !snapshot.bounty) return null;
  return { name: wanted.name, msLeft: snapshot.bounty.msLeft, rewardMass: snapshot.bounty.rewardMass, self: wanted.guid === selfGuid };
}
