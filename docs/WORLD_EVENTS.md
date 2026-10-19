# World events

Things that happen to a whole room now and then, apart from what the snakes do. **Lightning** is the first. The
pieces are built to be reused by the next ones (earthquakes that open fissures, procedurally generated challenge paths).

## Lightning

Each room, at a random moment, picks a spot of the map. The spot is **shown to the players for 1.5 s** (a dashed rim at the
full radius and a bright ring that grows to meet it, flickering white at the end), then it is **struck**:

- A body segment inside the radius is destroyed. It breaks off like a segment broken by a shot: the snake loses one
  segment of mass (`SnakeRules.MassPerSegment`) and that mass falls as a pellet where the segment was. Anyone can eat it.
- A head inside the radius kills the snake. Its whole mass falls as pellets along the body, as in any other death.
- A snake never loses body below its starting length to lightning (the same rule as for shots). Only a head hit kills.
- A snake with the **invulnerable** effect (see [PERK_SCRIPTS.md](PERK_SCRIPTS.md)) is not touched at all.
- A segment counts when the middle of its square is within the radius. The edge counts.
- The world wraps, but a strike does not: a spot at the edge of the map reaches only that side.

After a strike the next one is scheduled a random number of seconds from `MinIntervalSeconds` to `MaxIntervalSeconds` later.
A room without players has no lightning and forgets its schedule; a room that gets its first player starts a new interval.

### Settings

Admin page, tab **Gameplay**, card **World events** (`worldEvents` in the gameplay config; `WorldEventConfiguration` in the
game server's `appsettings.json` until the first fetch from the manager, like perks and prizes).

| Setting | Default | Meaning |
| --- | --- | --- |
| `enabled` | on | Switches every world event off. |
| `lightning.enabled` | on | Switches lightning off. |
| `lightning.minIntervalSeconds` / `maxIntervalSeconds` | 30 / 90 | Wait between strikes, drawn evenly between the two. |
| `lightning.radius` | 90 | World units a strike reaches (1 to 1000). A snake segment is 15 units. |
| `lightning.telegraphMilliseconds` | 1500 | How long the spot is shown before the strike (100 to 10000). |
| `lightning.rooms` | every room | Only in these rooms (code, name or id). |
| `lightning.excludedRooms` | none | Not in these rooms. Wins over `rooms`. |

Values that cannot work (an interval of 0, a longest wait shorter than the shortest, a radius of 0, a blank room) are refused
on save, with the reason shown, and nothing changes. Changes apply to a running game server within a few seconds; a warning
already on screen is struck as planned unless lightning was switched off, which takes it down without a strike.

## How it is built

| Piece | Where | Job |
| --- | --- | --- |
| `WorldEventOptions`, `LightningOptions` | `Domain.Game/Rules/WorldEventOptions.cs` | The settings. |
| `WorldEventRules` | `Domain.Game/Rules/WorldEventRules.cs` | Pure. `Step` moves a room's `LightningState` on (nothing, show a spot, strike it); `Resolve` says which segments and whether the head a strike reaches. Random numbers come from an injected function, the clock is passed in. |
| `WorldEventRunner` | `Domain.Game/Services/WorldEventRunner.cs` | Per room, in the style of `PrizeSpawner` and `PerkWorld`. Reads the clock (`TimeProvider`), keeps the state of each room, puts the warning in `IGameState.Strikes` and calls `IGameStateService.StrikeArea` when it is due. |
| `GameStateService.StrikeArea` | `Domain.Game/Services/GameStateService.cs` | Applies a strike: skips the invulnerable and the dead, breaks segments (`DropBrokenSegment`), kills heads (`KillPlayer`). Returns the snakes it killed. |
| `GameplayConfig.WorldEvents` | `Domain.Game/Rules/GameplayConfig.cs` | Part of the config the manager keeps; `GameplayApplier` puts it on the running rules. |
| Snapshot `strikes` | `SnapshotBuilder`, `SnapshotEvent` | The warning for the client. |
| `LightningTracker`, `lightningWarning` | `Application.Ui/src/render/lightning.ts` | Client side: counts the warning down between snapshots, notices the strike, tells the HUD. |
| Renderer | `render/pixelRenderer.ts` | Draws the rim, the ring and the flash. |
| `LightningHint.vue` | `components/game` | The banner: "Lightning incoming!", or "Get out of the circle!" when the own head is inside. |

### Why the game loop, not the 1 s food worker

`PrizeSpawner` and `PerkWorld` run from `FoodGeneratorWorkerService`, once a second, which is fine for a prize that lives 45 s.
A lightning warning of 1.5 s would be struck up to a second late (the strike would come anywhere from 1.5 to 2.5 s after the
warning, so players could not trust the ring), and the snakes a strike kills must be told and removed by the same loop that
removes the ones the simulation kills (`GameWorkerService` sends `PlayerDied` and disconnects them). So `WorldEventRunner.Update`
is called by `GameWorkerService` once per frame per room, before the simulation step. It costs a dictionary lookup and a
comparison when nothing is due. Strikes are resolved once per frame, never concurrently for a room, like the simulation.

### On the wire

`SnapshotEvent.strikes` is left out while there are none. Each entry is `{ "x", "y", "r", "t" }`: the spot and the radius in
whole world units and the milliseconds left, at most 8 numbers a snapshot for the one warning there ever is. There is no id: like
food, the place is its identity, and the client calls a warning that disappears from the newest snapshot "struck" and flashes it.
The client counts `t` down with its own clock from the moment the snapshot arrived, so no clocks have to agree.

### Adding the next event

1. Add its options to `WorldEventOptions` (and its validation to `WorldEventRules`), its fields to `useAdminGameplay` and the admin card.
2. Give `WorldEventRules` a state/step pair like `LightningState`/`Step`: pure, clock and random passed in, tested.
3. Let `WorldEventRunner` run it (or add a runner beside it) and keep anything the clients must see in `IGameState`, then in the snapshot.
4. Draw it in the renderer. Keep the snapshot additions compact and left out while idle.
