# World events

Things that happen to a whole room now and then, apart from what the snakes do. There are three:

- **Lightning storm**: a burst of small strikes over a few seconds.
- **Meteor rain**: a shower of big, slow meteors over a few seconds. Lightning and meteors share one engine.
- **Turret boss**: a mini boss that sits in the room firing volleys until snakes shoot it down. It has its own state and runner.

The next ones (earthquakes that open fissures, procedurally generated challenge paths) are to be added beside them; see
[Adding the next event](#adding-the-next-event).

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

Every few minutes (`minIntervalSeconds` to `maxIntervalSeconds`, 3 to 7 by default) a room with at least `minPlayers` players gets a
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
  "minIntervalSeconds": 180,         // wait between turrets
  "maxIntervalSeconds": 420,
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

Values that cannot work (an interval of 0, a longest wait shorter than the shortest, a radius of 0, more than 40 strikes, a blank
room, an aimed share outside 0 to 1) are refused on save, with the reason shown, and nothing changes. Changes apply to a running
game server within a few seconds; warnings already on screen fall as planned, unless the event was switched off, which takes them
down without a strike. **At most 40 strikes are marked at once in a room**, whatever is going on
(`WorldEventRunner.MaxActiveStrikes`): a strike that would be the 41st is dropped, never shown and never falls. A storm itself has
at most 40. So snapshots stay small.

**Configs from before storms still load.** A lightning section without `minStrikes`, `maxStrikes` and `stormSeconds` is a storm of
one strike, as it was. The editor fills in the storm defaults (5 to 12 over 6 s, aimed 0.25) so they can be changed, and saving
makes it a storm. A config without `meteors` or `turret` gets their defaults, switched on.

## How it is built

| Piece | Where | Job |
| --- | --- | --- |
| `WorldEventOptions`, `LightningOptions`, `MeteorOptions`, `IStormSettings`, `WorldEventKind` | `Domain.Game/Rules/WorldEventOptions.cs` | The settings. Both events implement `IStormSettings` (interval, radius, warning, count, duration, aimed share, rooms), which is all the engine knows about them. |
| `WorldEventRules` | `Domain.Game/Rules/WorldEventRules.cs` | Pure. `Step(kind, state, now, ...)` moves a room's `StormState` on: it says which strikes to mark and which to carry out. `Resolve` says which segments and whether the head a strike reaches. Random numbers come from an injected function, the clock is passed in, the living heads are passed in for aiming. |
| `WorldEventRunner` | `Domain.Game/Services/WorldEventRunner.cs` | Per room and kind, in the style of `PrizeSpawner` and `PerkWorld`. Reads the clock (`TimeProvider`), keeps the state, puts each mark in `IGameState.Strikes` and calls `IGameStateService.StrikeArea` when its warning is over. |
| `GameStateService.StrikeArea` | `Domain.Game/Services/GameStateService.cs` | Applies a strike (above). Returns the snakes it killed. |
| `GameplayConfig.WorldEvents` | `Domain.Game/Rules/GameplayConfig.cs` | Part of the config the manager keeps; `GameplayApplier` puts it on the running rules. |
| Snapshot `strikes` | `SnapshotBuilder`, `SnapshotEvent` | The warnings for the client. |
| `TurretOptions`, `TurretState`, `WorldEventRules.TurretStep`, `TurretKilled`, `TurretIsHit` | `Domain.Game/Rules` | The turret: pure scheduling, spawn, expiry, volleys (the angles of each shot and the turn), and whether a shot touches it. |
| `WorldBoss` | `Library.Commons.Game.Domain/Entities/WorldBoss.cs` | The boss standing in a room (`IGameState.Boss`): where, size, hit points, the reward, the last snake to hit it. |
| `TurretRunner` | `Domain.Game/Services/TurretRunner.cs` | Per room, from the game loop each frame, next to `WorldEventRunner`: spawns and removes the turret, calls `FireTurret` for each volley, and notices that snakes brought it down. |
| `GameStateService.FireTurret`, `ResolveBossHits` | `Domain.Game/Services/GameStateService.cs` | Creates the shots (owned by the boss) and lets snakes' shots hurt it; the simulation calls `ResolveBossHits` every step, after the shots move. The reward is given there. |
| `StrikeTracker`, `strikeWarning` | `Application.Ui/src/render/strikes.ts` | Client side: counts every warning down between snapshots, notices the strike, tells the HUD. |
| Renderer | `render/pixelRenderer.ts` | Draws the lightning rim, ring and flash, and the meteor shadow, fall and impact. |
| `bossCompass`, `armPixels`, `hpBarPixels` | `render/boss.ts` | The arrow to the boss, the pixels of its barrels and of its hit point bar. |
| `BossHint.vue` | `components/game` | The banner for the boss: an arrow to it, its hit points and the distance. |
| `StrikeHint.vue` | `components/game` | The banner: "Lightning incoming!", "Meteors incoming!", or "... Get out of the circle!" when the own head is inside one. |
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

### Adding the next event

1. If it comes as strikes on spots (a hail, a fire), add a `WorldEventKind`, an options record that implements `IStormSettings`,
   a line in `WorldEventOptions.Of`, a `WorldStrikeKind` and a way for the client to draw it. Nothing else changes.
2. If it is something else (a fissure that stays open, a path), give `WorldEventRules` its own state and step beside `StormState`:
   pure, clock and random passed in, tested. Let `WorldEventRunner` (or a runner beside it) run it, and keep anything the
   clients must see in `IGameState`, then in the snapshot.
3. Add its options to `WorldEventOptions` (and their validation to `WorldEventRules`), its fields to `useAdminGameplay` and a card
   to `WorldEventsEditor.vue`.
4. Draw it in the renderer. Keep the snapshot additions compact and left out while idle.
