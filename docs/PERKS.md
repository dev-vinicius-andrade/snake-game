# Perks and the prize

## Perks

A snake earns perks by eating. Each perk unlocks once the snake has eaten a number of foods in its life and is lost when it dies. Everything is set in the game server's `PerkConfiguration` section (`Configurations/appsettings.json`, or env vars such as `PerkConfiguration__Enabled=false`).

| Field | Meaning |
| --- | --- |
| `Enabled` | Turn all perks off with `false`. |
| `ShotSpreadDegrees` | Angle between neighbouring shots of a volley (default 12, must be in (0, 90)). |
| `BurstIntervalMilliseconds` | Time between the shots of a burst (default 80). |
| `Perks` | The list of perks. Left empty, the built-in set is used. |

Each perk has `Id`, `Name`, `FoodsRequired` (at least 1), `Effect` and `Amount`:

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

Perks of the same effect add up. The snake's earned perk ids are sent in the snapshot (`perks`, `foodsEaten`) and shown on the HUD.

Built-in set: magnet (6 foods), swift (8), quick-shot (12), twin-shot (15), armor (20), quick-reload (25), burst-shot (30), deep-lungs (35), regeneration (40), auto-fire (45), triple-shot (50), piercing-shot (60), turbo (70). A `Perks` list in the configuration replaces the whole set, so leave it out to get the built-in one.

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
| `Rooms`, `ExcludedRooms` | all | Rooms the kind appears in (code, name or id); exclusions win. |
| `SpawnIntervalSeconds` | 60 | From a prize being taken or lost to the next one. |
| `LifetimeSeconds` | 45 | How long an untaken prize stays. |
| `Value` | 20 | Mass gained by eating it. |
| `FoodsCredit` | 8 | Foods it counts for, toward perks. |
| `MinPlayers` | 2 | Players needed in the room. |

Rules live in `Domain/Domain.Game/Rules/PerkRules.cs` and `PrizeRules.cs`; `PrizeSpawner` applies them to a room once a second from the food generator worker.

## Controls

Left click shoots and holding the right mouse button boosts, next to Space and Shift. A game controller works as well: left stick steers, d-pad turns, A / bumper / right trigger shoots, B / left bumper / left trigger boosts.
