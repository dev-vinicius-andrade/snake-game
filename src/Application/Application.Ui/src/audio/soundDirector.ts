import { offsetTo } from "@/render/prizeCompass";
import { DEFAULT_HEAD_SIZE } from "@/render/pixelRenderer";
import type { FloatingDamage, GamePoint, GameSnapshot, MazeSnapshot, SnakeSnapshot } from "@/types/game";
import type { SoundEngine } from "./soundEngine";
import type { SoundId } from "./sounds";

/** A prize that vanished within this many head sizes of the own head was eaten by it. */
const PRIZE_REACH = 3 * DEFAULT_HEAD_SIZE;

const livingOwn = (snapshot: GameSnapshot, selfGuid: string): SnakeSnapshot | undefined =>
  snapshot.snakes.find((snake) => snake.guid === selfGuid && snake.isAlive);

/** A cooldown perk the snake could use before and has to wait for now was used in between. */
const abilityUsed = (before: SnakeSnapshot, after: SnakeSnapshot): boolean =>
  (after.abilities ?? []).some((ability) => ability.readyMs > 0 && (before.abilities ?? []).some((was) => was.id === ability.id && was.readyMs <= 0));

/** Whether a point (world units) lies within the grid of the maze. */
function insideMaze(maze: MazeSnapshot, point: GamePoint): boolean {
  const blocks = (cells: number) => (cells * (maze.corridor + 1) + 1) * maze.unit;
  const dx = point.x - maze.position.x;
  const dy = point.y - maze.position.y;
  return dx >= 0 && dy >= 0 && dx < blocks(maze.columns) && dy < blocks(maze.rows);
}

/**
 * Turns what changes between snapshots, and the damage and death notices, into sounds. It holds no audio itself: it asks a
 * `SoundEngine`. Only the own snake is heard (its meals, shots, boosts, wounds), plus world events everyone sees.
 */
export class SoundDirector {
  private previous: GameSnapshot | null = null;
  private bossGuid: string | null = null;
  private heard = new WeakSet<FloatingDamage>();

  constructor(private readonly engine: SoundEngine) {}

  /** Forget the last snapshot, e.g. after joining another room, so its differences are not played. */
  reset() {
    this.previous = null;
    this.bossGuid = null;
  }

  observe(snapshot: GameSnapshot, selfGuid: string | null | undefined) {
    const before = this.previous;
    this.previous = snapshot;
    this.bossGuid = snapshot.boss?.guid ?? null;
    if (!before) return;
    for (const id of this.differences(before, snapshot, selfGuid ?? "")) this.engine.play(id);
  }

  /** Plays the sounds of damage events not heard yet; the list may be handed over again and again. */
  damage(events: FloatingDamage[], selfGuid: string | null | undefined) {
    for (const event of events) {
      if (this.heard.has(event)) continue;
      this.heard.add(event);
      const id = this.damageSound(event, selfGuid);
      if (id) this.engine.play(id);
    }
  }

  died() {
    this.engine.play("die");
  }

  private damageSound(event: FloatingDamage, selfGuid: string | null | undefined): SoundId | null {
    if (selfGuid && event.targetGuid === selfGuid) {
      // The kill that ends the own snake is the death sound's business.
      if (event.kind === "kill") return null;
      if (event.kind === "saved") return "saved";
      return event.kind === "broke" ? "broke" : "hurt";
    }
    if (selfGuid && event.shooterGuid === selfGuid) {
      if (event.kind === "kill") return "kill";
      return event.targetGuid === this.bossGuid ? "turretHit" : "hit";
    }
    return null;
  }

  private differences(before: GameSnapshot, after: GameSnapshot, selfGuid: string): SoundId[] {
    return [...this.ownSounds(before, after, selfGuid), ...this.worldSounds(before, after), ...this.mazeSounds(before, after, selfGuid)];
  }

  private ownSounds(before: GameSnapshot, after: GameSnapshot, selfGuid: string): SoundId[] {
    const sounds: SoundId[] = [];
    const wasOwn = livingOwn(before, selfGuid);
    const own = livingOwn(after, selfGuid);
    if (!wasOwn || !own) return sounds;

    if (own.foodsEaten > wasOwn.foodsEaten) sounds.push(this.mealSound(before, after, own));
    const known = new Set(before.shots.map((shot) => shot.guid));
    if (after.shots.some((shot) => shot.ownerGuid === selfGuid && !known.has(shot.guid))) sounds.push("shoot");
    if (own.isBoosting && !wasOwn.isBoosting) sounds.push("boost");
    if (abilityUsed(wasOwn, own)) sounds.push("ability");
    return sounds;
  }

  /** A prize that was next to the head and is gone was eaten; any other meal is plain food. */
  private mealSound(before: GameSnapshot, after: GameSnapshot, own: SnakeSnapshot): SoundId {
    const left = new Set(after.foods.map((food) => food.guid));
    const prizeEaten = before.foods.some((food) => {
      if (food.kind !== "prize" || left.has(food.guid)) return false;
      const offset = offsetTo(own.head, food.position, after.room);
      return Math.hypot(offset.x, offset.y) <= PRIZE_REACH;
    });
    if (!prizeEaten) return "eat";
    return before.maze ? "mazePrize" : "prize";
  }

  private worldSounds(before: GameSnapshot, after: GameSnapshot): SoundId[] {
    const sounds: SoundId[] = [];

    const stillComing = new Set(after.strikes.map((strike) => `${strike.kind}:${strike.guid}`));
    const fallen = new Set(before.strikes.filter((strike) => !stillComing.has(`${strike.kind}:${strike.guid}`)).map((strike) => strike.kind));
    if (fallen.has("lightning")) sounds.push("lightning");
    if (fallen.has("meteor")) sounds.push("meteor");

    const mines = new Set((after.mines ?? []).map((mine) => mine.guid));
    if ((before.mines ?? []).some((mine) => !mines.has(mine.guid))) sounds.push("mine");

    if (!before.bounty && after.bounty) sounds.push("bountyStart");
    else if (before.bounty && !after.bounty) {
      const wanted = before.bounty.guid;
      if (!after.snakes.some((snake) => snake.guid === wanted && snake.isAlive)) sounds.push("bountyClaimed");
    }

    if ((before.showerMsLeft ?? 0) <= 0 && (after.showerMsLeft ?? 0) > 0) sounds.push("shower");
    if ((before.rushMsLeft ?? 0) <= 0 && (after.rushMsLeft ?? 0) > 0) sounds.push("rush");
    return sounds;
  }

  private mazeSounds(before: GameSnapshot, after: GameSnapshot, selfGuid: string): SoundId[] {
    if (!before.maze && after.maze) return ["mazeOpen"];
    if (!before.maze || !after.maze) return [];
    const wasOwn = livingOwn(before, selfGuid);
    const own = livingOwn(after, selfGuid);
    if (!wasOwn || !own) return [];
    const was = insideMaze(before.maze, wasOwn.head);
    const now = insideMaze(after.maze, own.head);
    if (now && !was) return ["mazeEnter"];
    // Walls cost mass and the server sends no notice for it: a snake that shrinks in the maze without boosting has hit one.
    if (now && own.path.length < wasOwn.path.length && !own.isBoosting && !wasOwn.isBoosting) return ["mazeWall"];
    return [];
  }
}
