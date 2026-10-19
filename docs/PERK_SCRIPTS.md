# Perk scripts

A perk normally does what its **effects** say (see [PERKS.md](PERKS.md)). A **script** lets an administrator write the logic the
effects cannot say: "two more shots while the snake is longer than 40 segments", "heal a little each time it eats", "armor only
while boosting". Scripts are JavaScript, kept in the perk on the admin page (Gameplay tab) and edited there.

## Writing one

On the admin page, **Gameplay** tab: open a perk and choose **Add a script**. A code editor (CodeMirror) opens with a small
example. **Check script** asks the server whether the script would be accepted (it parses it and runs it once with a sample),
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

Both functions are optional.

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
```

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

### `room`

`players`, `name` (may be empty), `code` (may be empty), `width`, `height`.

### `food` (in `onEat`)

`value` (mass it gave) and `isPrize`.

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

### What `onEat` may return

| Field | Range | Meaning |
| --- | --- | --- |
| `mass` | 0 to 50 | extra mass for the snake |
| `heal` | 0 to 500 | HP spread over the most damaged segments |

## Where it runs

On the game server, in the rooms the perk names. The manager keeps the scripts with the rest of the gameplay config and the game
servers fetch them; a saved script applies within a few seconds, without a restart. Players never receive a script: the public
perk list has names and effects only.
