import { SCRIPT_EXAMPLE } from "../composables/useAdminGameplay";

/** A script an admin can start from. The name and the sentence that says what it does are in the language files, under its id. */
export interface ScriptTemplate {
  id: string;
  code: string;
}

export const SCRIPT_TEMPLATES: readonly ScriptTemplate[] = [
  { id: "starter", code: SCRIPT_EXAMPLE },
  {
    id: "comeback",
    code: `// A snake that has just come back gets a faster boost for its first 10 seconds.
function modify(snake, room) {
  if (snake.ageMs > 10000) return {};
  return { boostSpeedBonus: 0.5 };
}
`,
  },
  {
    id: "berserk",
    code: `// Hurt snakes shoot more; healthy ones mend.
function modify(snake, room) {
  if (snake.health < 0.3) return { extraShots: 1 };
  if (snake.health > 0.6) return { regenPerSecond: 3 };
  return {};
}
`,
  },
  {
    id: "hunter",
    code: `// Killing a snake as big as you, or bigger, gives extra mass.
function onKill(snake, victim, room) {
  return { mass: victim.length >= snake.length ? 20 : 0 };
}
`,
  },
  {
    id: "lastStand",
    code: `// With two players or fewer left in the room, the magnet pulls food in.
function modify(snake, room) {
  return room.players <= 2 ? { magnetRadius: 120 } : {};
}
`,
  },
  {
    id: "luckyFood",
    code: `// One food in twenty counts triple.
function onEat(snake, food, room) {
  return Math.random() < 0.05 ? { mass: food.value * 2 } : {};
}
`,
  },
  {
    id: "spawnProtection",
    code: `// Nothing can hurt a snake in its first 30 seconds.
function modify(snake, room) {
  return { invulnerable: snake.ageMs < 30000 };
}
`,
  },
  {
    id: "killStreak",
    code: `// Five kills in a row, without dying, give 8 seconds of invulnerability.
// snake.state is remembered for as long as the snake lives.
function onKill(snake, victim, room) {
  snake.state.streak = (snake.state.streak || 0) + 1;
  if (snake.state.streak >= 5) {
    snake.state.shieldUntil = snake.now + 8000;
    snake.state.streak = 0;
  }
}

function modify(snake, room) {
  return { invulnerable: snake.now < (snake.state.shieldUntil || 0) };
}
`,
  },
  {
    id: "freeShots",
    code: `// Eating a prize makes shots cost nothing for 15 seconds.
function onEat(snake, food, room) {
  if (food.isPrize) snake.state.freeUntil = snake.now + 15000;
}

function modify(snake, room) {
  return { freeShots: snake.now < (snake.state.freeUntil || 0) };
}
`,
  },
  {
    id: "prizeDrop",
    code: `// Every 3 minutes a prize of the kind "perk-prize" drops in the room.
// Make that kind in the Prizes card, and give it a long interval so it only comes from here.
// onTick runs about once a second for the room; room.state is remembered until the room is empty.
function onTick(room) {
  if (room.now - (room.state.last || 0) < 180000) return {};
  room.state.last = room.now;
  return { dropPrize: "perk-prize" };
}
`,
  },
];
