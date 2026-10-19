# Perk scripts

A perk normally does what its **effects** say (see [PERKS.md](PERKS.md)). A **script** lets an administrator write the logic the
effects cannot say: "two more shots while the snake is longer than 40 segments", "heal a little each time it eats", "armor only
while boosting". Scripts are JavaScript, kept in the perk on the admin page (Gameplay tab) and edited there.

## Writing one

On the admin page, **Gameplay** tab: open a perk and choose **Add a script**. A code editor (CodeMirror) opens with a small
example. The editor suggests what a script can use as you type (`snake.`, `room.`, `food.`, `victim.`, the keys a function may
return, and the four functions after `function`). **Start from a template** replaces the script with one of ready-made examples
(comeback, berserk, hunter, last stand, lucky food, spawn protection, kill streak shield, free shots, prize drop); undo in the
editor brings the old text back. **Check script** asks the server whether the script would be accepted (it parses it and runs it once with a sample),
and says why not if it would not. **Save** checks it again and refuses a script that fails.

## Safe by default

- A perk with a script is **off everywhere** until it names rooms (the *Only in these rooms* field of the perk: invite code,
  name or id). It runs in those rooms and nowhere else. Perks without a script work in every room unless they name rooms too.
- A script cannot reach the server: no files, no network, no .NET. It only sees the snake and the room it is given, and it can
  only return numbers.
- Every call has a time limit (5 ms), a limit on how much it may do and how much memory it may use. A script that breaks a
  limit, throws, or returns something invalid is skipped for that call, and after three failures in a row the perk's script is
  switched off until the config is saved again. The effects of the perk keep working.
- A script is checked when you save: it must parse, and it is run once with a sample snake. A script that fails the check is
  refused, with the reason.

## What a script can define

Every function is optional, but a script needs at least one.

```js
// Called a few times a second for each snake that has the perk. Returns more effects.
function modify(snake, room) {
  return {
    extraShots: snake.length > 40 ? 2 : 0,
    regenPerSecond: snake.boosting ? 0 : 2,
  };
}

// Called each time the snake eats food (or the prize).
function onEat(snake, food, room) {
  return { heal: 15, mass: food.isPrize ? 5 : 0 };
}

// Called each time the snake kills another one (shot in the head, or the other ran into it).
function onKill(snake, victim, room) {
  return { mass: victim.length >= snake.length ? 20 : 0 };
}

// Called about once a second for the room, whoever is in it (not for a snake). May drop a prize.
function onTick(room) {
  return room.players >= 3 ? { dropPrize: "perk-prize" } : {};
}
```

`modify` runs for each snake that has the perk, `onEat` and `onKill` for the snake that ate or killed, and `onTick` once a second
for each room where the perk works, with or without snakes that have it (an empty room is skipped).

### `snake`

| Field | Meaning |
| --- | --- |
| `name` | the player's nickname |
| `length` | body segments |
| `mass` | the score (what boosting and shooting spend) |
| `foodsEaten` | foods eaten in this life |
| `health` | average segment health, 0 to 1 |
| `lowestHealth` | health of the most damaged segment, 0 to 1 |
| `boosting` | true while the boost control is held |
| `perks` | ids of the perks the snake has |
| `now` | the server clock in milliseconds (the same for everyone) |
| `ageMs` | milliseconds since this snake came into the room |
| `kills` | kills in this life |
| `state` | what the script remembers about this snake, see below |

### `room`

`players`, `name` (may be empty), `code` (may be empty), `width`, `height`, `now` (milliseconds), and in `onTick` also `state`.

### `food` (in `onEat`)

`value` (mass it gave) and `isPrize`.

### `victim` (in `onKill`)

`name`, `length` and `mass` of the snake that was killed.

### Remembering things: `state`

`snake.state` (and `room.state` in `onTick`) is a plain object the script can change in place. It is kept between calls, **per
perk**: for a snake until it dies or leaves, for a room until the room is empty. It is limited to 2000 characters of JSON; a call
that leaves it bigger fails like any other failure, and a state that is not an object starts over. Use it for streaks and timers:

```js
function onKill(snake, victim, room) {
  snake.state.streak = (snake.state.streak || 0) + 1;
  if (snake.state.streak >= 5) snake.state.shieldUntil = snake.now + 8000;
}
function modify(snake, room) {
  return { invulnerable: snake.now < (snake.state.shieldUntil || 0) };
}
```

### What `modify` may return

Every field is optional. Numbers are clamped to the range shown; anything else is ignored. The result is **added** to what the
perk's effects and the snake's other perks give, except the factors, which are **multiplied**.

| Field | Range | Like the effect |
| --- | --- | --- |
| `extraShots` | 0 to 4 | ExtraShots |
| `burstExtraShots` | 0 to 6 | BurstShot |
| `boostSpeedBonus` | 0 to 3 | BoostSpeed |
| `boostLengthFactor` (multiplied) | 1 to 4 | EfficientBoost |
| `reloadFactor` (multiplied; below 1 is faster) | 0.2 to 1 | FasterReload |
| `shotSpeedFactor` (multiplied) | 1 to 4 | ShotSpeed |
| `damageFactor` (multiplied; below 1 is tougher) | 0.2 to 1 | Armor |
| `regenPerSecond` | 0 to 50 | Regeneration |
| `magnetRadius` | 0 to 300 | Magnet |
| `autoFire` | true or false | AutoFire |
| `piercing` | true or false | PiercingShot |
| `invulnerable` | true or false | shots and other snakes cannot hurt the snake (it still hits them) |
| `freeShots` | true or false | shooting costs no mass, and works at zero mass |

### What `onEat` and `onKill` may return

| Field | Range | Meaning |
| --- | --- | --- |
| `mass` | 0 to 50 | extra mass for the snake |
| `heal` | 0 to 500 | HP spread over the most damaged segments |

### What `onTick` may return

`dropPrize`: the id of a prize kind, or a list of ids (at most 3 different ones per call). The prize appears at a random spot of the
room for the kind's lifetime. Nothing happens when the kind does not exist, is off, is not for this room, or already has a prize on
the map. A kind of prize is made in the **Prizes** card (it may give a perk); to have it come only from scripts, give it a very long
spawn interval.

## Icon and description

Each perk can have an **icon** (a Material Design Icons name such as `mdi-shield`, picked from a list or typed) and a **description**
(one sentence, at most 200 characters) that players see in the perk's tooltip. Without an icon the perk uses the icon of its first
effect, so a perk made only of a script should have one.

## Where it runs

On the game server, in the rooms the perk names. The manager keeps the scripts with the rest of the gameplay config and the game
servers fetch them; a saved script applies within a few seconds, without a restart. Players never receive a script: the public
perk list has names and effects only.
