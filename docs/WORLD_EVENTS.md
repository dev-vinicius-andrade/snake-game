# World events

Things that happen to a whole room now and then, apart from what the snakes do. There are nine:

- **Lightning storm**: a burst of small strikes over a few seconds.
- **Meteor rain**: a shower of big, slow meteors over a few seconds. Lightning and meteors share one engine.
- **Turret boss**: a mini boss that sits in the room firing volleys until snakes shoot it down. It has its own state and runner.
- **Shrinking safe zone**: a circle that closes in on the room; snakes outside it lose mass and die. It has its own state and runner.
- **Fruit shower**, **bounty**, **mine field** and **sugar rush**: see [below](#fruit-shower-bounty-mine-field-and-sugar-rush).

- **Maze**: a procedurally generated maze rises in a clear part of the room, with a big prize at the far end. See [Maze](#maze).

The next ones (earthquakes that open fissures) are to be added beside them; see [Adding the next event](#adding-the-next-event).

## What a storm does

Each room, at a random moment, starts a storm: a random number of strikes (from the fewest to the most), whose moments are drawn
over the storm's duration. **Each strike is marked on the map on its own**, at its own random spot, for its own warning time, and
then falls. A share of them (`aimedShare`, 0 is none) is aimed near a random living snake's head, within 60 % of the radius of it,
so they are felt; the rest land anywhere in the room. After the last strike has fallen the next storm is scheduled a random number
of seconds later (`minIntervalSeconds` to `maxIntervalSeconds`). A room without players has no events and forgets its schedule; a
room that gets its first player starts a new interval.

A strike, whether lightning or meteor, does this where it falls (`GameStateService.StrikeArea`, the same code for both):

- A body segment inside the radius is destroyed. It breaks off like a segment broken by a shot: the snake loses one segment of mass
  (`SnakeRules.MassPerSegment`) and that mass falls as a pellet where the segment was. Anyone can eat it.
- A head inside the radius kills the snake. Its whole mass falls as pellets along the body, as in any other death.
- A snake never loses body below its starting length to a strike (the same rule as for shots). Only a head hit kills.
- A snake with the **invulnerable** effect (see [PERK_SCRIPTS.md](PERK_SCRIPTS.md)) is not touched at all, and neither is a dead one.
- A segment counts when the middle of its square is within the radius. The edge counts.
- The world wraps, but a strike does not: a spot at the edge of the map reaches only that side.

| | Lightning storm | Meteor rain |
| --- | --- | --- |
| Strikes per storm | 5 to 12 | 6 to 14 |
| Storm lasts | 6 s | 8 s |
| Radius | 90 | 130 |
| Warning per strike | 1.5 s | 2 s |
| Between storms | 45 to 120 s | 120 to 300 s |
| Aimed share | 0.25 | 0.2 |
| Looks like | dashed rim, growing yellow ring, white flicker and flash | striped dark shadow, dashed rim, growing orange ring, a meteor with a streak falling in, fire-coloured flash |

A segment is 15 units, a room 1920 x 1080 by default.

## Turret boss

Every few minutes (`minIntervalSeconds` to `maxIntervalSeconds`, 2 to 5 by default) a room with at least `minPlayers` players (1 by default) gets a
**turret**: a square of `size` world units (60) that appears somewhere in the room, at least half its size from the edges, and sits
still. The first volley comes 3 s after it appears, so players can see it and get ready. Then every `fireIntervalMilliseconds`
(900) it fires a **volley**: `arms` shots (6), evenly spread round it. After each volley the whole pattern turns by `spinDegrees` (11),
so the volleys sweep round it like a bullet-hell turret.

- **Its shots** are slow (`shotSpeed` 0.5 of a snake shot, so 225 units a second and dodgeable) and fly to the edge of the room.
  They hurt like any shot: a head hit kills, a body hit damages and may break a segment. They are drawn in red and never hurt the turret.
- **Snakes shoot it.** Every shot that touches its square takes one hit point (`hitPoints`, 40) and is spent; a piercing shot hits it
  once and goes on. The shooter sees the damage numbers.
- **Bringing it down.** The snake that lands the last shot gets the reward: `rewardMass` (60) falls as food in a ring round the
  turret for anyone to grab, and, if `grantsPerk` names a perk, the snake gets that perk for the rest of its life (the perk must
  exist in the list). Then the next turret is scheduled from that moment.
- **Leaving.** A turret nobody brings down leaves after `lifetimeSeconds` (90); the next is scheduled from then. A room that loses
  all its players loses its turret.
- It does not hurt snakes by itself: touching it is safe. Only its shots do harm.

The client shows the body with a glowing core, its barrels turning with it, a hit point bar above it that goes from green to
amber to red, and a banner with an arrow to it, its hit points and the distance.

## Shrinking safe zone

Every 5 to 10 minutes (`minIntervalSeconds` to `maxIntervalSeconds`) a room with at least `minPlayers` players gets a safe zone, in
three phases:

1. **Warning** (`warningSeconds`, 15). The safe spot is marked on the map with a dashed circle of the final radius, a banner counts
   down, and nobody is harmed. The zone itself covers the whole room.
2. **Closing** (`shrinkSeconds`, 60). The edge of the zone closes in evenly on the safe spot, from covering the whole room to
   `finalRadius` (160). The ground just outside the edge is tinted red in dots, the edge is a bright ring, and the spot where it will
   end up stays marked.
3. **Holding** (`holdSeconds`, 30). It stays at its smallest. Then it goes, and the next is scheduled from that moment.

While it closes and holds, every `damageIntervalMilliseconds` (1000) **each snake whose head is outside it pays**: it loses
`damageMass` (4) mass, which falls as food right where the snake is, so snakes inside can grab it. **A snake with no mass left that is
outside dies** (its body is only the starting length, so there is nothing left to drop). A snake that has the invulnerable effect pays
nothing, a dead one is left alone. A snake whose head is inside is never touched, whatever its tail does.

The safe spot is drawn anywhere in the room. The world wraps, so the zone does too: distance is the short way round (a head at the
right edge is near a centre at the left edge), and the zone at its start, with a radius of half the diagonal of the room, covers it all.

The client draws the dashed final circle, the ring, the danger tint and a banner ("Safe zone in 12s: head for the circle", "The safe
zone is closing: stay inside", "Stay inside the circle"). While the own snake is outside the banner turns red, says it is losing mass,
and the edges of the screen glow red.

## Fruit shower, bounty, mine field and sugar rush

Four more events, all timed by one pure scheduler (`EventSchedule`) and run alike (`ScheduledEventRunner`). Each comes every
`minIntervalSeconds` to `maxIntervalSeconds` in a room with at least `minPlayers` players, has `rooms` and `excludedRooms` like the
others, and can be started by an administrator on request (`fruitshower`, `bounty`, `mines`, `sugarrush`; the number of players is
ignored then). They have no calm/wild room choice yet: a room can only leave them out with `excludedRooms`.

- **Fruit shower** (a good thing). For `durationSeconds` (20) fruit falls at random spots, `fruitsPerSecond` (8) of it, through the
  food generator (`IGameStateService.GenerateFood`), so it is ordinary food that stays until eaten. It ignores the room's food limit.
  A frame never pours more than one second of fruit, so a stall does not flood the room. Banner: "Fruit shower!".
- **Bounty.** One random living snake is marked wanted for `durationSeconds` (90). A marker over its head, in every player's snapshot,
  shows it, with a banner saying who it is and the reward. **Killing it** (a head shot, or it running into the killer's body) pays the
  killer `rewardMass` (150) mass at once and `foodsCredit` (10) foods toward its perks, and takes the mark off. The mark also ends when
  the snake dies some other way (a strike, the zone), leaves, or the time is up; the next bounty is scheduled from then. Killing
  someone else, or the wanted snake killing itself, pays nothing. It needs `minPlayers` (3) so there is someone to hunt it.
- **Mine field.** A field puts `minMines` to `maxMines` (6 to 14) mines at random spots. Each is shown at once and is **armed** after
  `armMilliseconds` (2000), then **stays until a snake's head comes within `triggerRadius` (20) of it**. The field itself is over at
  once but its mines are not, so fields add up, at most 40 on the map (`WorldEventRules.MaxActiveMines`). Going off is like a strike
  with `radius` (60): every body segment of every snake in the blast breaks off and falls as a pellet (the head is never killed, and a
  snake never loses body below its starting length), and the snakes that lost mass see `-N`. An invulnerable snake neither sets one off
  nor is hurt. Mines vanish when the event is switched off or the room empties.
- **Sugar rush.** For `durationSeconds` (30) **boosting costs no mass** for every snake in the room, even one with none left
  (`IGameState.SugarRushEndsAt`, read by `GameStateService.MovePlayer`; `BoostRules.Step(free: true)`). Banner: "Sugar rush!".

```jsonc
"fruitShower": { "enabled": true, "minIntervalSeconds": 180, "maxIntervalSeconds": 420,
                 "durationSeconds": 20,                   // 1 to 60
                 "fruitsPerSecond": 8,                    // 1 to 30
                 "minPlayers": 1, "rooms": [], "excludedRooms": [] },
"bounty":      { "enabled": true, "minIntervalSeconds": 240, "maxIntervalSeconds": 600,
                 "durationSeconds": 90,                   // 5 to 3600
                 "rewardMass": 150,                       // 0 to 10000
                 "foodsCredit": 10,                       // 0 to 1000
                 "minPlayers": 3, "rooms": [], "excludedRooms": [] },
"mines":       { "enabled": true, "minIntervalSeconds": 150, "maxIntervalSeconds": 360,
                 "minMines": 6, "maxMines": 14,           // 1 to 40
                 "radius": 60,                            // blast, 1 to 1000
                 "triggerRadius": 20,                     // how close a head must come, 1 to 200
                 "armMilliseconds": 2000,                 // 0 to 60000
                 "minPlayers": 1, "rooms": [], "excludedRooms": [] },
"sugarRush":   { "enabled": true, "minIntervalSeconds": 240, "maxIntervalSeconds": 600,
                 "durationSeconds": 30,                   // 1 to 600
                 "minPlayers": 1, "rooms": [], "excludedRooms": [] }
```

A saved config from before these events has none of the four sections: it gets their defaults, switched on. In the database each has
its own table (`world_event_fruit_shower`, `world_event_bounty`, `world_event_mine_field`, `world_event_sugar_rush`, see
[DATABASE.md](DATABASE.md)), and the migration that adds them also writes the default row for each, so a database that already has a
saved config starts running them with the defaults; a config read from a database with no row for one of them gets its defaults too.

On the wire, in the snapshot (all left out while idle): `mines: [{ x, y, r, t }]` (spot, blast radius, milliseconds until armed, 0 once
armed; a mine that disappears from the newest snapshot was set off, which the client shows as an explosion), `bounty: { guid, t, m }`
(the wanted snake's guid in `snakes`, milliseconds left, reward mass), `shower: ms` and `rush: ms` (milliseconds left).

## Maze

Every `minIntervalSeconds` to `maxIntervalSeconds` (7 to 15 minutes by default) a room with at least `minPlayers` players (2) gets a
**maze**: a region of `columns` x `rows` cells (12 x 8) that stands for `durationSeconds` (240) in a part of the room, with one big
prize at its far end. Snakes race through it. It is an event inside a room, not a game mode, but the pieces (the generator and the wall
collision) know nothing about rooms and could carry a race mode later.

- **Shape.** A branching maze with dead ends and a few loops (6 walls per hundred cells are opened after the carving). `twistiness`
  (0 to 100, 60) is the chance at each step that a corridor turns instead of going straight on: 0 makes long straight halls, 100
  turns at every chance. The outer wall is closed except for one **entrance** on a random side. The **prize** is in the cell farthest
  from the entrance by the way through the maze.
- **Sizes are in head sizes** (`SnakeConfiguration.HeadSize`, 15 world units). A wall is one head size thick and a corridor is
  `corridorHeads` wide (4, at least 3 so a long snake can turn). The maze is a grid of blocks of one head size.
- **Where.** At least 4 head sizes from every edge of the room, and not within 2 head sizes of a living snake's head: a spot is drawn
  up to 30 times, and if none is clear the maze does not rise this time. A room too small for the size asked for gets a smaller maze
  (down to 3 x 3 cells); one that cannot hold even that gets none.
- **Walls stop heads and cost mass, and never kill.** A head that would enter a wall is held where it was, or **slides** along the wall
  if it can (so a snake pressing on a wall at an angle keeps moving). The snake loses `wallMassLoss` (2) mass, at most once every half
  second, never more than it has, and it falls as food where the head is. A long snake cannot die of walls. An invulnerable snake is held
  but pays nothing. A head already inside a wall (a snake there when the maze rose, or that spawned there) may walk out, for free.
- **Walls stop shots.** A shot that reaches a wall vanishes; it is looked at every half block, so a fast shot cannot jump over a wall.
  Walls cannot be broken. They are static: mines, the zone and strikes work as before.
- **Fruit.** `foodPercent` (25) of the cells get a fruit when the maze rises (never the entrance cell or the prize cell). Set 0 for none.
  Fruit stays when the maze goes, like any food.
- **The prize** is a normal prize (food of kind prize, prize id `maze`, which no kind in the prize list may use) worth `prizeMass`
  (200) mass and `prizeFoodsCredit` (20) foods toward perks, and it can give a perk (`prizeGrantsPerk`, a perk of the list). The prize
  **compass** points at it while the maze stands, even when another prize is nearer.
- **The end.** The maze goes when the prize is taken, or when `durationSeconds` is up (then the prize goes with it), or when the room
  empties. The next one is scheduled from then. The walls simply disappear.
- A banner counts the time down ("Maze! Race to the prize at the far end", warmer and "Walls cost mass" while the own snake is inside).
- An administrator can start one at once (`maze`; the number of players is ignored), like the other events.

The server and the client build the same walls from the seed: the snapshot carries only the corner, the cells, the corridor width, the
twistiness, the seed and the size of a block. The generator is `MazeGenerator` (`Domain.Game/Rules`) and `generateMaze`
(`Application.Ui/src/game/maze.ts`); both use only integer arithmetic (mulberry32 for the random numbers) and are checked against one
shared test vector, `src/Tests/Shared/maze-vector.json` (the walls of five mazes as text). Change the algorithm in both languages, then
rewrite the vector with `MAZE_VECTOR_WRITE=1 dotnet test --filter MazeGeneratorTests` and run the UI tests. Collision is one lookup of a
block per check (`WorldMaze.BlocksSquare` looks at the at most four blocks a head overlaps).

```jsonc
"maze": { "enabled": true, "minIntervalSeconds": 420, "maxIntervalSeconds": 900,
          "durationSeconds": 240,               // 10 to 3600
          "columns": 12, "rows": 8,             // cells, 3 to 30 each
          "corridorHeads": 4,                   // corridor width in head sizes, 3 to 12
          "twistiness": 60,                     // 0 to 100
          "wallMassLoss": 2,                    // mass lost on a wall, 0 to 1000
          "foodPercent": 25,                    // cells with a fruit, 0 to 100
          "minPlayers": 2,
          "prizeMass": 200,                     // 1 to 10000
          "prizeFoodsCredit": 20,               // 0 to 1000
          "prizeGrantsPerk": "magnet",          // optional: a perk of the list
          "prizeColor": "#ffd23f",
          "rooms": [], "excludedRooms": [] }
```

A saved config from before the maze has no `maze` section and gets its defaults, switched on. In the database it is the table
`world_event_maze` (one row, see [DATABASE.md](DATABASE.md)); the migration `WorldEventMaze` writes the default row, and a config read
from a database with no such row gets the defaults too. On the wire, in the snapshot (left out while there is none): `maze: { x, y, c,
r, k, v, s, u, t }` (corner, cells across and down, blocks across a corridor, twistiness, seed, world units per block, milliseconds left).

## What a strike costs, and how it is felt

- **A splash on the body.** When a strike falls, every segment of every snake that was inside it throws bits off: white and yellow
  for lightning, burning orange for a meteor, with a bright blob where the segment was. Everyone in the room sees it, since it is drawn
  from what the snapshots say (the segments that were inside the strike in the snapshot before it fell), not from a message to the
  snake that was hit.
- **Minus N at the head.** The snake that lost mass, to lightning, a meteor or the zone, is told how much, as a red `-N` that floats up
  from its head (a `DamageDealt` event of kind `lost`, sent only to that snake; the shooter is an empty guid). For a strike it is the
  mass of the segments that broke, for a head hit all of the mass, for the zone the mass it took.

## Perks and tiers

Kills and bosses count toward perks too, see [PERKS.md](PERKS.md): killing a snake counts for `killFoodsCredit` foods (3), and
bringing the turret down for its own `foodsCredit` (15). Foods are what perks and their tiers are earned with.

## Gameplay per room

Everything above is the default every room inherits, set by the administrator. **A room can change it for itself**, and whoever makes
a private room picks this when making it (the **Gameplay** panel of the **Create private room** dialog). Each feature has a choice:

| Choice | Meaning |
| --- | --- |
| Default (`inherit`) | The room changes nothing: it is what the administrator set. |
| Off | The feature is off in this room. |
| On (`normal`) | On, with the administrator's numbers, even where the administrator switched it off. |
| Calm | On, the administrator's numbers made gentler: it comes half as often, with fewer, smaller strikes and more warning. |
| Wild | On, the administrator's numbers made harsher: twice as often, more and bigger strikes, less warning. |

Perks and prizes only know default, off and on. Lightning, meteors, turret and zone know all five. A preset is a way of changing the
numbers of the administrator, never a set of numbers of its own, so changing the default changes the calm and wild rooms with it. What
each preset does to the numbers is in `WorldEventPresets`; the results are always kept inside what the settings accept (a wild storm
never has more than 40 strikes).

An administrator can change the choices of any room later, and **start an event in it right now**, from **Gameplay** in the row of
the room in the admin rooms list (for trying an event out, or livening a quiet room up). Starting works when the event is on in the room,
somebody is in it, and one of the kind is not already going; a turret or a zone started this way ignores the number of players it
asks for. The manager keeps what a room chose with the room (it comes back in the heartbeat of the game server), and passes changes on to
the game server, which applies them at once.

```
POST /rooms                          { id, name?, maxPlayers, gameplay?: { turret: "off", lightning: "wild", ... } }   (host)
PUT  /admin/rooms/{id}/gameplay      { turret: "off", ... }                              (admin key; only what differs from the default)
POST /admin/rooms/{id}/events/{kind}  kind: lightning | meteors | turret | zone | fruitshower | bounty | mines | sugarrush | maze         (admin key; 204, 409 not now, 404, 502)
```

## Settings

Admin page, tab **Gameplay**, the **World events** form (`WorldEventsEditor.vue`: `worldEvents` in the gameplay config;
`WorldEventConfiguration` in the game server's `appsettings.json` until the first fetch from the manager, like perks and prizes).

```jsonc
"worldEvents": {
  "enabled": true,                   // switches every world event off
  "lightning": {
    "enabled": true,
    "minIntervalSeconds": 45,        // wait between storms, drawn evenly between the two
    "maxIntervalSeconds": 120,
    "radius": 90,                    // world units a strike reaches, 1 to 1000
    "telegraphMilliseconds": 1500,   // warning before each strike, 100 to 10000
    "minStrikes": 5,                 // strikes per storm, 1 to 40
    "maxStrikes": 12,
    "stormSeconds": 6,               // over how many seconds the strikes begin, 1 to 120
    "aimedShare": 0.25,              // 0 to 1, share aimed near a random living snake
    "rooms": [],                     // only in these (code, name or id); empty: every room
    "excludedRooms": []              // not in these; wins over rooms
  },
  "meteors": {
    "enabled": true,
    "minIntervalSeconds": 120,
    "maxIntervalSeconds": 300,
    "radius": 130,
    "telegraphMilliseconds": 2000,
    "minMeteors": 6,                 // meteors per rain, 1 to 40
    "maxMeteors": 14,
    "rainSeconds": 8,
    "aimedShare": 0.2,
    "rooms": [],
    "excludedRooms": []
  }
}
```

The turret has its own section, with the same room switches:

```jsonc
"turret": {
  "enabled": true,
  "minIntervalSeconds": 120,         // wait between turrets
  "maxIntervalSeconds": 300,
  "lifetimeSeconds": 90,             // how long it stays when nobody brings it down, 10 to 3600
  "hitPoints": 40,                   // shots to bring it down, 1 to 10000
  "size": 60,                        // side of its square in world units, 20 to 300
  "fireIntervalMilliseconds": 900,   // between volleys, 100 to 10000
  "arms": 6,                         // shots per volley, 1 to 16
  "spinDegrees": 11,                 // the volley turns by this each time, -360 to 360
  "shotSpeed": 0.5,                  // 1 is the speed of a snake shot, 0.1 to 3
  "minPlayers": 2,                   // players needed in the room before it appears
  "rewardMass": 60,                  // food dropped where it falls, 0 to 1000
  "grantsPerk": "magnet",            // optional: a perk of the list given to the snake that brings it down
  "rooms": [], "excludedRooms": []
}
```

The safe zone has its own section too:

```jsonc
"zone": {
  "enabled": true,
  "minIntervalSeconds": 300,         // wait between zones
  "maxIntervalSeconds": 600,
  "warningSeconds": 15,              // safe spot shown, nobody harmed, 1 to 600
  "shrinkSeconds": 60,               // time to close, 5 to 3600
  "holdSeconds": 30,                 // time it holds when closed, 0 to 3600
  "finalRadius": 160,                // world units that stay safe, 30 to 2000
  "damageIntervalMilliseconds": 1000, // how often a snake outside pays, 200 to 10000
  "damageMass": 4,                   // mass lost each time, 1 to 1000
  "minPlayers": 2,
  "rooms": [], "excludedRooms": []
}
```

Values that cannot work (an interval of 0, a longest wait shorter than the shortest, a radius of 0, more than 40 strikes, a blank
room, an aimed share outside 0 to 1) are refused on save, with the reason shown, and nothing changes. Changes apply to a running
game server within a few seconds; warnings already on screen fall as planned, unless the event was switched off, which takes them
down without a strike. **At most 40 strikes are marked at once in a room**, whatever is going on
(`WorldEventRunner.MaxActiveStrikes`): a strike that would be the 41st is dropped, never shown and never falls. A storm itself has
at most 40. So snapshots stay small.

**Configs from before storms still load.** A lightning section without `minStrikes`, `maxStrikes` and `stormSeconds` is a storm of
one strike, as it was. The editor fills in the storm defaults (5 to 12 over 6 s, aimed 0.25) so they can be changed, and saving
makes it a storm. A config without `meteors`, `turret` or `zone` gets their defaults, switched on.

## How it is built

| Piece | Where | Job |
| --- | --- | --- |
| `RoomGameplay`, `FeaturePreset`, `WorldEventPresets`, `WorldEventRules.OptionsFor` | `Library.Commons.Game.Domain/Entities/RoomGameplay.cs`, `Domain.Game/Rules/WorldEventPresets.cs` | The choices of a room, and what they make of the administrator's options. `OptionsFor(room)` gives the options a runner uses for a room, worked out once while neither side changes. |
| `RoomGameplayService`, `EventTriggers` | `Application.Game.Server/Services/RoomGameplayService.cs` | What the manager can ask of a room: new choices, start an event (`PUT /rooms/{id}/gameplay`, `POST /rooms/{id}/events/{kind}` on the game server). |
| `WorldEventOptions`, `LightningOptions`, `MeteorOptions`, `IStormSettings`, `WorldEventKind` | `Domain.Game/Rules/WorldEventOptions.cs` | The settings. Both events implement `IStormSettings` (interval, radius, warning, count, duration, aimed share, rooms), which is all the engine knows about them. |
| `WorldEventRules` | `Domain.Game/Rules/WorldEventRules.cs` | Pure. `Step(kind, state, now, ...)` moves a room's `StormState` on: it says which strikes to mark and which to carry out. `Resolve` says which segments and whether the head a strike reaches. Random numbers come from an injected function, the clock is passed in, the living heads are passed in for aiming. |
| `WorldEventRunner` | `Domain.Game/Services/WorldEventRunner.cs` | Per room and kind, in the style of `PrizeSpawner` and `PerkWorld`. Reads the clock (`TimeProvider`), keeps the state, puts each mark in `IGameState.Strikes` and calls `IGameStateService.StrikeArea` when its warning is over. |
| `GameStateService.StrikeArea` | `Domain.Game/Services/GameStateService.cs` | Applies a strike (above). Returns the snakes it killed. |
| `GameplayConfig.WorldEvents` | `Domain.Game/Rules/GameplayConfig.cs` | Part of the config the manager keeps; `GameplayApplier` puts it on the running rules. |
| Snapshot `strikes` | `SnapshotBuilder`, `SnapshotEvent` | The warnings for the client. |
| `TurretOptions`, `TurretState`, `WorldEventRules.TurretStep`, `TurretKilled`, `TurretIsHit` | `Domain.Game/Rules` | The turret: pure scheduling, spawn, expiry, volleys (the angles of each shot and the turn), and whether a shot touches it. |
| `ZoneOptions`, `ZoneState`, `WorldEventRules.ZoneStep`, `RadiusAt`, `PhaseOf`, `PhaseEnd`, `IsOutsideZone` | `Domain.Game/Rules` | The zone: pure scheduling, the three phases, the radius at any moment, and whether a point is outside (the short way round the world). |
| `WorldZone` | `Library.Commons.Game.Domain/Entities/WorldZone.cs` | The zone in a room (`IGameState.Zone`): where, how far it reaches now and at the end, the phase and when it ends. Replaced every frame as it closes. |
| `ZoneRunner` | `Domain.Game/Services/ZoneRunner.cs` | Per room, from the game loop each frame: keeps the state, updates `IGameState.Zone`, and every damage interval calls `GameStateService.ApplyZoneDamage`. Returns the snakes that died of it, which the loop tells and removes with the others. |
| `GameStateService.ApplyZoneDamage` | `Domain.Game/Services/GameStateService.cs` | Takes mass from every snake outside (as food where it is), kills the one with none, skips the invulnerable. |
| `WorldBoss` | `Library.Commons.Game.Domain/Entities/WorldBoss.cs` | The boss standing in a room (`IGameState.Boss`): where, size, hit points, the reward, the last snake to hit it. |
| `TurretRunner` | `Domain.Game/Services/TurretRunner.cs` | Per room, from the game loop each frame, next to `WorldEventRunner`: spawns and removes the turret, calls `FireTurret` for each volley, and notices that snakes brought it down. |
| `GameStateService.FireTurret`, `ResolveBossHits` | `Domain.Game/Services/GameStateService.cs` | Creates the shots (owned by the boss) and lets snakes' shots hurt it; the simulation calls `ResolveBossHits` every step, after the shots move. The reward is given there. |
| `StrikeTracker`, `strikeWarning` | `Application.Ui/src/render/strikes.ts` | Client side: counts every warning down between snapshots, notices the strike, tells the HUD. |
| Renderer | `render/pixelRenderer.ts` | Draws the lightning rim, ring and flash, and the meteor shadow, fall and impact. |
| `bossCompass`, `armPixels`, `hpBarPixels` | `render/boss.ts` | The arrow to the boss, the pixels of its barrels and of its hit point bar. |
| `zoneWarning`, `isOutsideZone` | `render/zone.ts` | What the banner needs: the phase, the time left, and whether the own snake is outside. |
| `ZoneHint.vue` | `components/game` | The banner for the zone, and the red glow while the own snake is outside. |
| `BossHint.vue` | `components/game` | The banner for the boss: an arrow to it, its hit points and the distance. |
| `StrikeHint.vue` | `components/game` | The banner: "Lightning incoming!", "Meteors incoming!", or "... Get out of the circle!" when the own head is inside one. |
| `FruitShowerOptions`, `BountyOptions`, `MineFieldOptions`, `SugarRushOptions`, `IScheduledEvent` | `Domain.Game/Rules/ScheduledEventOptions.cs` | The settings of the four newer events; validated in `WorldEventRules` (`MaxActiveMines` is 40). |
| `EventSchedule`, `EventWindow` | `Domain.Game/Rules/EventSchedule.cs` | Pure timing shared by them: wait, begin (for a duration, or at once), end. Clock and random numbers are passed in. |
| `ScheduledEventRunner`, `FruitShowerRunner`, `BountyRunner`, `MineFieldRunner`, `SugarRushRunner` | `Domain.Game/Services` | Per room, from the game loop every frame: begin, tick, clear (also when the room empties or the event is switched off), and `Trigger`. They keep what the clients must see in `IGameState` (`Mines`, `Bounty`, `FruitShowerEndsAt`, `SugarRushEndsAt`). |
| `GameStateService.ResolveMines`, `PayBounty`, free boost in `MovePlayer` | `Domain.Game/Services/GameStateService.cs` | What a mine does when touched, what killing the wanted snake pays (from `RegisterKill`), and boosting for free during a sugar rush. |
| `MazeOptions`, `MazeGenerator`, `MazeLayout`, `MazeRunner` | `Domain.Game/Rules/ScheduledEventOptions.cs`, `MazeGenerator.cs`, `Domain.Game/Services/MazeRunner.cs` | The maze: its settings (validated in `WorldEventRules`), the pure seeded generator, and the runner (a `ScheduledEventRunner`) that finds a clear spot, raises the maze with its prize and fruit, and takes it down. |
| `WorldMaze`, `IMazeWalls` | `Library.Commons.Game.Domain/Entities/WorldEffects.cs` | The maze in a room (`IGameState.Maze`): corner, size, seed, walls, the prize id, the wall cost; `BlocksPoint` and `BlocksSquare`. |
| `GameStateService.HoldAtWalls`, `ShotHitsWall` | `Domain.Game/Services/GameStateService.cs` | Holds a head back from walls (sliding, the mass cost, never a death) in `MovePlayer`; removes a shot that reaches a wall in `MoveObstacles`. |
| `generateMaze`, `mazeLayoutOf`, `visibleBlocks`, `wallBlocks`, `mazeWarning` | `Application.Ui/src/game/maze.ts`, `render/maze.ts` | The TypeScript port of the generator, and what the renderer and the banner need from it. |
| `MazeHint.vue` | `components/game` | The banner for the maze. |
| `WorldEventsEditor.vue` | `components/admin` | The admin form, one card per event. Props: `{ gameplay: ReturnType<typeof useAdminGameplay> }`. |

### Why the game loop, not the 1 s food worker

`PrizeSpawner` and `PerkWorld` run from `FoodGeneratorWorkerService`, once a second, which is fine for a prize that lives 45 s.
A warning of 1.5 s would be struck up to a second late, so players could not trust the ring, and the snakes a strike kills must be
told and removed by the same loop that removes the ones the simulation kills (`GameWorkerService` sends `PlayerDied` and
disconnects them). So `WorldEventRunner.Update` is called by `GameWorkerService` once per frame per room, before the simulation
step. It costs a few dictionary lookups when nothing is due. Strikes are resolved once per frame, never concurrently for a room,
like the simulation.

### On the wire

`SnapshotEvent.strikes` is left out while there are none. Each entry is `{ "x", "y", "r", "t", "k" }`: the spot and the radius in
whole world units, the milliseconds left, and the kind (`0` lightning, `1` meteor). There is no id: like food, the place is its
identity, and the client calls a warning that disappears from the newest snapshot "struck" and flashes it, in the colours of its
kind. The client counts `t` down with its own clock from the moment the snapshot arrived, so no clocks have to agree.

`SnapshotEvent.boss` is left out while there is no boss. It is `{ "guid", "x", "y", "s", "hp", "a", "n" }`: the id (its shots have it
as their `owner`, which is how the client draws them red), the middle in whole world units, the size, the hit points left in whole
percents (at least 1), the angle of the first arm in whole degrees and the number of arms.

`SnapshotEvent.zone` is left out while there is no zone. It is `{ "x", "y", "r", "f", "p", "t" }`: the middle in whole world units, how far
the zone reaches now (`r`) and at the end (`f`), the phase (`0` warning, `1` closing, `2` holding) and the milliseconds left in the phase.
The radius is sent as it is at the moment of the snapshot, so it closes in steps of the snapshot rate (30 a second), which is smooth enough.

### Adding the next event

1. If it comes as strikes on spots (a hail, a fire), add a `WorldEventKind`, an options record that implements `IStormSettings`,
   a line in `WorldEventOptions.Of`, a `WorldStrikeKind` and a way for the client to draw it. Nothing else changes.
2. If it is something else (a fissure that stays open, a path), give `WorldEventRules` its own state and step beside `StormState`:
   pure, clock and random passed in, tested. Let `WorldEventRunner` (or a runner beside it) run it, and keep anything the
   clients must see in `IGameState`, then in the snapshot.
3. Add its options to `WorldEventOptions` (and their validation to `WorldEventRules`), its fields to `useAdminGameplay` and a card
   to `WorldEventsEditor.vue`.
4. Draw it in the renderer. Keep the snapshot additions compact and left out while idle.
