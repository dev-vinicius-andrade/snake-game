# Perks and the prize

## Perks

A snake earns perks by eating. Each perk unlocks once the snake has eaten a number of foods in its life and is lost when it dies. Everything is set in the game server's `PerkConfiguration` section (`Configurations/appsettings.json`, or env vars such as `PerkConfiguration__Enabled=false`).

| Field | Meaning |
| --- | --- |
| `Enabled` | Turn all perks off with `false`. |
| `ShotSpreadDegrees` | Angle between neighbouring shots of a volley (default 12, must be in (0, 90)). |
| `BurstIntervalMilliseconds` | Time between the shots of a burst (default 80). |
| `ExtraLifeKeepShare` | Share (0..1) of its mass a snake keeps when an extra life saves it (default 0.5). |
| `ExtraLifeProtectSeconds` | Seconds a saved snake cannot be hurt (default 3, 0 to 60). |
| `Perks` | The list of perks. Left empty, the built-in set is used. |

Each perk has `Id`, `Name`, `FoodsRequired` (at least 1), `Effect` and `Amount` (or a list of `Effects`, each with its `Amount` and, for event resistance, a `Target`), and for a cooldown ability `CooldownSeconds` and `DurationSeconds`:

| Effect | What `Amount` does |
| --- | --- |
| `ExtraShots` | Shots added to every volley. A volley costs 1 mass however many shots it has. |
| `BoostSpeed` | Added to the boost speed factor (default 1.8). |
| `FasterReload` | Share (0..1) taken off the shot cooldown. The cooldown never drops below a fifth. |
| `EfficientBoost` | Share (0..1) more time boosting for each mass spent. |
| `BurstShot` | Shots fired one after the other each time you shoot, `BurstIntervalMilliseconds` apart. The whole burst costs 1 mass. |
| `ShotSpeed` | Added to the shot speed factor (0.5 makes shots half as fast again). |
| `AutoFire` | Holding the shoot control keeps shooting. |
| `PiercingShot` | Shots pass through snakes and hit each body once. |
| `Armor` | Share (0..1) less damage from body shots. Never below a fifth. |
| `Regeneration` | HP per second healed on the most damaged segment. |
| `Magnet` | World units around the head from which food is pulled in. The prize is never pulled. |
| `Precision` | Share (0..1) taken off the angle between the shots of a volley (`ShotSpreadDegrees`), so they fly closer together. Several precisions multiply; the fan never closes below a tenth. |
| `ExtraLife` | Lives (a whole number, at most 5): see [Extra lives](#extra-lives). |
| `EventResistance` | Share (0..1) less mass lost to lightning, meteors, mines and the zone. The entry's `target` (`lightning`, `meteors`, `mines` or `zone`) limits it to one event; empty is all. See [Event resistance](#event-resistance). |
| `Plunder` | Share (0..1) of the mass of a snake you kill that you take at once, on top of the food it drops. The snake that kills it by shooting or by being run into both count. |
| `ShieldBurst` | A **cooldown ability**: for the perk's `DurationSeconds` nothing can hurt the snake. `Amount` only needs to be above 0. |
| `Dash` | A cooldown ability: for the duration the snake is `Amount` faster (1 is twice as fast), without spending mass. |
| `HealPulse` | A cooldown ability: heals `Amount` HP at once, spread over the most damaged segments. Instant, so it needs no duration. |
| `VacuumBurst` | A cooldown ability: for the duration food within `Amount` world units is pulled to the head (like a magnet, but only while it is on). |

Perks of the same effect add up. The snake's earned perk ids are sent in the snapshot (`perks`, `foodsEaten`) and shown on the HUD.

Built-in set: magnet (6 foods), swift (8), quick-shot (12), twin-shot (15), dash (18), armor (20), steady-aim (22), quick-reload (25), stormproof (28), burst-shot (30), shield-burst (33), deep-lungs (35), heal-pulse (38), regeneration (40), vacuum (42), auto-fire (45), triple-shot (50), second-chance (55), piercing-shot (60), plunder (65), turbo (70). A `Perks` list in the configuration replaces the whole set, so leave it out to get the built-in one.

## Tiers

A perk can be **upgraded**. The perk as it is, is tier I. It can have up to nine more tiers (II to X, ten in all); each asks for more foods than
the one before and multiplies everything the perk does by its `multiplier`, counted from tier I:

```jsonc
{ "id": "magnet", "foodsRequired": 6, "effect": "Magnet", "amount": 60,
  "tiers": [ { "foodsRequired": 15, "multiplier": 1.5 },    // tier II: radius 90
             { "foodsRequired": 30, "multiplier": 2 } ] }    // tier III: radius 120
```

- A snake is at the highest tier whose foods it has reached. A perk it was given (by a prize or the turret) with few foods is at tier I.
- **Foods are not only food.** What a snake has done counts as foods toward perks and so toward tiers: eating, a prize's `foodsCredit`,
  **killing another snake** (`killFoodsCredit` on the perks, 3 by default), **bringing the turret down** (`foodsCredit` on the turret,
  15 by default) and **killing the wanted snake of a bounty** (`foodsCredit` on the bounty, 10 by default, on top of the kill credit). A kill or a boss can therefore lift a perk to the next tier.
- Everything with an amount scales: radius, speed, regeneration, shots (rounded to whole shots). Switches (auto fire, piercing) have
  no strength, so tiers do not change them. A share (armor, quick reload, deep lungs) never goes past 95 % however high the tier.
- Scripted perks are not scaled; they decide for themselves.
- Tiers can also be reached with a reward that lifts a perk **beyond its foods**: see [Tier surges](#tier-surges).
- The built-in perks that have amounts to scale have a tier II at two and a half times their foods (1.5 times as strong) and a tier III
  at five times (twice as strong).
- Players see the tier as a numeral on the perk's icon, and the tooltip says what the perk does at that tier and the foods the next one asks for.
- On the admin page, in the perk's panel: **Add a tier** gives it the next foods and strength to edit, **Remove tier** takes one away.
  A tier must ask for more foods than the perk and than the tier before it; the strength is above 0 and at most 10.

### Reward-only tiers

A tier can be marked `RewardOnly` (admin: the **Reward only** switch on the tier). Eating never reaches it; only a tier surge lifts a perk into it, so it can hold the strength that nothing but a rare reward should give. Its `Multiplier` works like any other tier's, and players see it as the next numeral on the icon.

Rules (checked when the config is saved): the perk itself (tier I) is never reward-only, and reward-only tiers must come after every eatable tier (tiers are ordered by foods; a reward-only tier still needs more foods than the one before, the number only keeps the order). A perk has at most ten tiers in all.

Example: a perk with 5 eatable tiers and 2 reward-only ones (7 in all).

| Snake | Tier |
| --- | --- |
| At its 5th tier by eating, no surge, eats 100000 more foods | 5 (eating never goes past the last eatable tier) |
| Same snake gets a surge of 1 | 6 |
| Surge of 2 | 7 |
| Surge of 5 | 7 (stops at the last tier) |
| At its 3rd tier by eating, surge of 2 | 5 |
| Same, but later eats up to the 5th | 7 (the surge stays on top) |

The tooltip of a perk whose next tier is reward-only says so instead of asking for foods.

## Extra lives

`ExtraLife` perks and rewards (see below) give a snake lives. When something would kill it (a shot on the head, running into a body, a
lightning or meteor hit on the head, the zone with no mass left) a life is used instead:

- it keeps `ExtraLifeKeepShare` of its mass (default 0.5); the rest falls as food where it was;
- its whole body is healed;
- nothing can hurt it for `ExtraLifeProtectSeconds` (default 3), and it blinks with a ring while that lasts;
- the attacker gets no kill, no kill credit and no bounty: the snake did not die;
- the owner sees a green **1UP** float up from the head (a `DamageDealt` of kind `saved`, `damage` is the lives left) and hears a rising jingle; the HUD shows a heart with the lives left next to the perks.

Lives come from the perk (`ExtraLife` amount, scaled and rounded by tier, at most 5) plus the lives rewards gave this life (at most 5), minus the lives already used. All of it ends with the life.

## Event resistance

`EventResistance` takes mass losses from world events down by its share (shares of several perks multiply, as armor does, and never reach
95 %). It spares **mass**, so:

- the zone takes less each time (never less than 1 while there is something to take);
- lightning, meteors and mines break fewer body segments: the share of the broken segments that is spared is not broken at all (rounded to the nearest, the nearest to the impact break first);
- a lightning or meteor hit **on the head still kills**. Resistance does not make a snake immune; an extra life does.

## Cooldown perks

A perk with `ShieldBurst`, `Dash`, `HealPulse` or `VacuumBurst` is an **ability**: it does nothing until used, then recharges. Set on the
perk (admin: the perk's panel): `CooldownSeconds` (at least 1; the time from use to ready again) and, for all but the heal pulse,
`DurationSeconds` (at least 1; how long it is on). Several abilities in one perk are used and recharge together; tiers make them stronger
(the amount), not faster. A new ability is ready at once.

How it is used (`UseAbility` on the game hub):

- **keyboard** `E` or `Q`, **controller** `X` or `Y`, **touch** the `POWER` button (shown only while the snake has an ability). One press uses every ability that is ready; holding does not repeat.
- **eyes and face**: the eye and face controls have no spare gesture (blink or far gaze is shoot and boost, mouth and eyebrows the same). While eye control is on the client tells the server (`SetAutoAbilities`) to use the abilities **by itself when they would help**: a shield burst when an enemy shot is within 120 units, a dash while boosting, a heal pulse when a segment is under half health, a vacuum burst when three or more foods are in its reach. Nothing else changes for them.

Cues: the HUD shows each ability as a perk icon. **Ready** has a white border, **on** is cyan, **recharging** is grey with the seconds left; the tooltip says it in words. The server sends `ab` for each snake (`id`, `r` ms to ready, `a` ms to the end of the effect). A snake under a shield burst or a saved life is drawn with a blinking cyan ring (`prot` in the snapshot, milliseconds left), so everyone sees it. A shield burst is the same protection as a life saved: shots and other snakes pass it by, it still hits them.

## Rewards

Beyond foods and mass, a prize, the turret, a bounty or the maze prize can give a **reward**. The perk they name (`GrantsPerk` / `PrizeGrantsPerk`) stays as it was; the reward adds, all of it optional and off by default, so a config from before rewards still works:

| Field | What it does |
| --- | --- |
| `GrantsRandomPerk` | A perk the snake does not have, picked at random among the perks that are on and work in the room. Nothing if it has them all. |
| `ExtraLives` | 0 to 5 lives, added to the snake's lives (the total never passes 5). |
| `TierSurgeMin`, `TierSurgeMax` | A tier surge of between the two tiers (0 to 10; `Max` below `Min` counts as `Min`). 0 and 0: none. |
| `TierSurgePerk` | The perk to lift. Empty: a random perk the snake has that can still go up. If it is named and cannot go up (not owned, or at its last tier), nothing happens. |
| `BonusMass` | Mass given at once, 0 to 10000. |
| `Mend` | Heals the whole body. |

Where it is set: `Reward` on every prize kind and on the turret and the bounty, `PrizeReward` on the maze. On the admin page it is the **Reward** box of each. The order of giving: the named perk, mass, lives, mend, the random perk, then the tier surge (so a surge can lift the perk that was just given).

### Tier surges

A surge lifts a perk **beyond what the foods give**. The rule, one for all: the tier of a perk is

> `min(last tier of the perk, tier the foods give + surge)`

where the tier the foods give is the highest **eatable** tier whose foods the snake has reached (the first for a perk that was given with few foods; tiers marked reward-only are never counted), and `surge` is the total tiers rewards added to that perk in this life. So a surge is not lost as the snake eats on (the foods tier keeps rising on top of it), a perk already at its last tier is not lifted further, a surge never gives a tier the perk does not have (a perk with one tier has nothing to lift), and a surge on a perk the snake does not own does nothing. A surge is kept per perk and capped at the perk's number of tiers minus one. It ends with the life.

## More rewards and perks that came with this: why

- **Precision** tightens a fan, so a twin or triple shot can be aimed from a distance. It is the answer to the fan being a weakness of the big shot perks.
- **Extra life** and **event resistance** are the defence against being thrown out of a good run by something the snake cannot shoot back at.
- **Cooldown abilities** give a choice of *when*, which a passive perk cannot. Shield burst (survive), dash (escape or close in), heal pulse (recover mid-fight), vacuum burst (a harvest burst).
- **Plunder** rewards kills with mass, a "lucky drop" without a random number: the winner of a big fight gets a share of the prize as well as the food.
- **Bonus mass** and **mend** as reward kinds are the smallest rewards that are still worth a trip: a mass dividend from the maze, a full heal from the turret.
- Not done, on purpose: revive at a body segment (a life already keeps half the mass; a second respawn rule would be a second rule to teach), damage reflect (needs the shooter's body in a shot hit; the armor and the shield already answer the same need).

## Icons

Perk icons are from [game-icons.net](https://game-icons.net) (by Lorc, Delapouite and contributors, CC BY 3.0, credited in the footer of the
site), written `game-icons:name`, for example `game-icons:magnet`. Each built-in effect has its own. The pack is not shipped: only the
few icons the game uses are, in `src/game/gameIcons.json`, written from `src/game/gameIconNames.json` by `npm run icons` (which reads
the `@iconify-json/game-icons` dev dependency). To offer another icon in the admin picker, add its name to the list, run `npm run icons`
and commit both files. A perk may also be given any Material Design Icons name (`mdi-skull`), typed; a name that is neither, or a game icon
that does not ship, is shown as a star. The server checks the shape of the name only.

## Perks per room

A perk works in every room unless it says otherwise, and a perk with a script works in no room until it names some. Three ways to
say it, all on the admin page:

- **Gameplay tab, each perk:** *Only in these rooms* (the perk works only there) and *Not in these rooms* (it works everywhere
  but there). A room is named by its invite code, its name or its id; the case does not matter. *Not in these rooms* wins.
- **Rooms tab, the Perks button of a room:** tick the perks that work in that room. It writes into the same two lists: a perk
  with a script gets the room in *Only in these rooms*, and an ordinary perk is switched off in a room by naming it in *Not in
  these rooms*.
- Rooms are forgotten when the game server restarts. A private room is named by its invite code, a public one by its id, so a
  perk list that names a room that no longer exists does nothing until that room is back.

## Editing it while the game runs (admin page)

Everything above, the perks and the prize, can be edited on the admin page (`/admin`, tab **Gameplay**) with the admin key. Save,
and every game server applies it within a few seconds; no restart, and players already in a room keep playing.

- The manager keeps the config and the game servers fetch it (`GET /gameplay`, every 5 s). **The manager's config wins** over
  the `PerkConfiguration` and `PrizeConfiguration` sections of the game server's `appsettings.json`, which only apply until the
  first fetch.
- Values the game could not run (a perk that needs 0 foods, a prize that never appears) are refused when you save, with the
  reason shown, and nothing changes.
- **Load defaults** puts the built-in perks and prize in the form; **Undo edits** goes back to what is saved. Neither saves.
- The config is kept in the file named by `GameplayConfiguration__StorePath`, so it survives restarts when that file is on a
  volume. The home lab deploy does this (a small volume at `/data`, see `deploy/homelab/README.md`). Without a path, a restart
  goes back to the built-in defaults.
- Players see their perks as icons with tooltips; the names and effects come from `GET /gameplay/perks` (public, with no scripts and no room names).
- A perk can have several effects and a script: see [PERK_SCRIPTS.md](PERK_SCRIPTS.md), which also says how to give a perk an icon and a description.
- Each perk has a **Perk on** switch. A perk that is off stays in the list but nobody gets it, not even from a prize; the per-room dialog shows it as off.
- The **Private rooms** card sets how long private rooms live: minutes an empty one stays open (`emptyPrivateRoomMinutes`, default 10) and the longest any private room lives (`maxPrivateRoomMinutes`, 0 is no limit). See [ROOMS.md](ROOMS.md).

- The **World events** card sets lightning, the first world event. See [WORLD_EVENTS.md](WORLD_EVENTS.md).

## The prize

Every so often a prize appears at a random spot of a room. It is worth a lot of mass and counts as several foods toward perks, so snakes fight over it. The HUD shows an arrow and the distance to the nearest prize. If nobody takes it, it disappears and the next one comes after the interval. A room needs at least `MinPlayers` players for a prize to appear.

There can be up to 10 **kinds** of prize (`Prizes` list), each with its own timing, mass, colour and rooms. A room has at most one prize of each kind on the map. `Enabled` on the section switches all kinds off; each kind has its own `Enabled` too. A kind can **give a perk** (`GrantsPerk`, the id of a perk): the snake that eats it gets that perk whatever foods it has eaten, as long as the perk is on and works in that room. Saving is refused when the perk does not exist. The old single-prize fields are still read and become one kind named `prize`.

Fields of a kind (`PrizeConfiguration:Prizes`):

| Field | Default | Meaning |
| --- | --- | --- |
| `Id`, `Name` | prize | Unique id, shown name. |
| `Enabled` | true | Turn this kind off with `false`. |
| `Color` | #ffd23f | Hex colour of the prize on the map. |
| `GrantsPerk` | none | Perk id given to the snake that eats it. |
| `Reward` | nothing | A random perk, lives, a tier surge, mass or a mend: see [Rewards](#rewards). |
| `Rooms`, `ExcludedRooms` | all | Rooms the kind appears in (code, name or id); exclusions win. |
| `SpawnIntervalSeconds` | 60 | From a prize being taken or lost to the next one. |
| `LifetimeSeconds` | 45 | How long an untaken prize stays. |
| `Value` | 20 | Mass gained by eating it. |
| `FoodsCredit` | 8 | Foods it counts for, toward perks. |
| `MinPlayers` | 2 | Players needed in the room. |

The prize of the [maze](WORLD_EVENTS.md#maze) is a prize too (food of kind prize, with its own `foodsCredit` and optional perk), but not one of these kinds: its id `maze` is reserved, so no kind in the list may use it, and `PrizeSpawner` leaves it to the maze.

Rules live in `Domain/Domain.Game/Rules/PerkRules.cs` and `PrizeRules.cs`; `PrizeSpawner` applies them to a room once a second from the food generator worker.

## Controls

Left click shoots and holding the right mouse button boosts, next to Space and Shift. A game controller works as well: left stick steers, d-pad turns, A / bumper / right trigger shoots, B / left bumper / left trigger boosts. `E` or `Q` (controller `X` or `Y`, the `POWER` button on a touch screen) uses the cooldown perks, see [Cooldown perks](#cooldown-perks).
