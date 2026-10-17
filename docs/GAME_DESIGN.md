# Game Design: Damage Model and Server Loop

Status: proposal, nothing implemented. All numbers are starting values, expose them in `SnakeConfiguration` and tune by playtest.

## 1. Vocabulary

| Term | Meaning |
|------|---------|
| mass `m` | Float. Fruits eaten and not yet spent. Source of truth for size, boost and shoot. |
| length `L` | Body segment count = `S0 + floor(m / F)`. `S0` = initial size (5), `F` = mass per segment (1). |
| segment | One body part. Has `hp` and derived `maxHp`. Index 0 is the head. |
| shot | Projectile fired by a snake. Costs `c` mass. |

## 2. Economy (fruits)

- Eat fruit of value `v`: `m += v`, then heal (section 4).
- Boost: while held and `L > S0`, spend `b` mass per second (start 2.0). Speed multiplier `x1.8`. Stops when `L == S0`.
- Shoot: spend `c` mass per shot (start 1.0), cooldown 250 ms. Not allowed when `L == S0`.
- Spent mass is not dropped (it is consumed). Boost may leave a small trail pellet worth 50% of spend (optional, tune later).
- Death: all of `m` drops as pellets around the body, spread along its segments. Pellet count capped (for example 40): pellet value = `m / count`, so food entities stay bounded. Killer gets nothing automatically; they must collect.

## 3. Segment HP

`H0 = 100` is the base segment HP. Bigger snakes get sturdier segments, so a big snake is slow to dismantle:

```
maxHp(L) = H0 * (1 + gamma * ln(max(L, S0) / S0))        gamma = 1.0
```

Each segment stores `hp` in `[0, maxHp]`. When `maxHp` rises because the snake grew, `hp` ratio is preserved (`hp = ratio * newMaxHp`) so growing does not silently heal nor hurt.

## 4. Shot damage

Damage applied to the hit **body** segment:

```
raw   = B * (Ls / Lt) ^ alpha          B = 40, alpha = 0.5
dmg   = clamp(raw, dMin, capFrac * maxHp(Lt))      dMin = 5, capFrac = 0.5
```

- `Ls` = shooter length at fire time, `Lt` = target length at hit time.
- Cap guarantees **no single shot can break a segment** (needs at least `ceil(1/capFrac) = 2` hits on the same segment). Because the cap scales with `maxHp(Lt)` big targets are never one-shot.
- Shots to break one segment (equal `hp = maxHp`):

| Shooter L | Target L | dmg | maxHp | shots |
|-----------|----------|-----|-------|-------|
| 10 | 10 | 40 | 169 | 5 |
| 40 | 40 | 40 | 308 | 8 |
| 5 | 40 | 14 | 308 | 22 |
| 20 | 80 | 20 | 377 | 19 |
| 80 | 20 | 80 | 239 | 3 |
| 40 | 5 | 50 (capped) | 100 | 2 |

Small snakes can still harass big ones, but cannot realistically cut them down. Big snakes shred small ones fast, which is fair because small ones die anyway on head collisions.

### Breaking a segment

When `hp <= 0`: the segment is removed, its mass `F` drops as a pellet at its position, the snake loses `F` mass, and the body closes up (segments behind move up on next move). Open question: alternative is slither-style cut (everything behind the broken segment is severed and drops). Current spec ("break the body and reduce its size") is read as the single-segment version, which is gentler and easier to balance.

### Head hits

- Shot overlaps a head: target dies immediately (death rules in section 2). No damage formula.
- Optional: spawn protection of ~2 s (no head kill, no body damage) to avoid spawn camping. Not in the rules given, add only if playtesting needs it.
- Friendly fire: a shot never hits its own shooter.

### Healing

On eating a fruit of value `v`: heal pool `P = healPerFruit * v` (start 30 HP per fruit). Apply to the segment with the **lowest `hp / maxHp` ratio** (tie: closest to head), up to its `maxHp`; leftover `P` flows to the next most damaged segment. Growth and healing both happen on the same fruit.

### Rendering contract

Server sends per-segment `hp` quantized to 0..255 of `maxHp` (one byte). Client colors segment `lerp(snakeColor, red, 1 - ratio)`. Server never sends colors for damage.

## 5. Collision rules (non-shot)

- Head vs another snake's body segment (any index >= 1): head owner dies.
- Head vs own body: ignored (slither rule). Revisit if it feels odd.
- Head vs head: both die. Deterministic and simple; tie-break by length is the alternative.
- Head vs fruit: eat. Fruit pickup radius slightly larger than head (+50%) for feel and for eye control.
- World edge: wrap-around today. Slither kills at the edge. Decision needed; wrap is easier for beginners and for eye control.

## 6. Server game loop

### Findings in current code (`GameWorkerService`, `GameStateService`)

1. `ProcessRooms` launches one `Task.Run` per player and calls `MoveObstacles` inside it, so every projectile moves N times per tick (N = player count).
2. Score and full `GameState` are broadcast per player per tick (`ScoreUpdated` inside the per-player task, N times). Wasteful.
3. Shared mutable state (`Players`, `Obstacles`, `Foods` dictionaries) is mutated from parallel tasks without locks.
4. Tick cadence is `Task.Delay(TickRate)` after processing, so effective rate drifts with load. No fixed timestep.
5. Collisions: only head vs fruit and head vs projectile. No head vs body. Projectiles kill on any contact. No HP.
6. Movement is 8-direction discrete, one path point per tick, tail removed with `Path.Remove(First())`. Growth only changes score, not length.
7. Death calls `DisconnectPlayer`, which also removes the player, so the client cannot show a death screen with a final state.

### Target design

One loop per room, single-threaded per room, rooms in parallel. Zero locks inside a room because only the loop touches its state; hub methods only enqueue input.

```
fixed timestep: sim 30 Hz (dt = 33.3 ms), snapshot broadcast 15 Hz, accumulator + Stopwatch
(no Task.Delay drift; if behind, run several sims, cap at 5 to avoid spiral)

per sim step (room):
 1. drain input queue          -> set desired direction / angle, boost flag, shoot flag
 2. apply abilities            -> boost spends mass, shoot spawns projectile (cooldown, mass check)
 3. move snakes                -> advance head, shift segments, apply growth/shrink from mass
 4. move projectiles           -> advance, expire by range/lifetime
 5. rebuild spatial hash       -> cell size ~ 2 * HeadSize, rebuilt each step
 6. resolve collisions         -> collect events first, apply after (order independent):
                                  a. projectile vs head       -> mark kill
                                  b. projectile vs body       -> damage event (formula section 4)
                                  c. head vs body / head vs head -> mark kill
                                  d. head vs fruit            -> eat event
 7. apply events               -> damage (break segments), eats (grow + heal), kills (drop pellets)
 8. spawn food                 -> top up to MaxFoods (fold in FoodGeneratorWorkerService)
 9. every 2nd step: build and broadcast snapshot
```

Why events-then-apply: with parallel or sequential in-place mutation, who dies in a head-to-head depends on iteration order. Collecting then applying makes outcomes order independent.

### Network

- Input messages: `{ angle | direction, boost, shoot, seq }`. Eye tracking client sends gaze angle (continuous), so add angle input next to the 8 discrete directions. Server clamps turn rate per step (`RotationSpeed` already in README config), which also smooths noisy gaze.
- Snapshot: tick id, snakes (id, head pos, segment positions, per-segment hp byte, length), projectiles, fruits as add/remove deltas, leaderboard top 10 at 1 Hz. Binary (MessagePack, already supported by SignalR) over JSON.
- Interest management only if the world grows beyond one screen. At 1920x1080 skip it.
- Death event carries final score, then the player becomes a spectator for a respawn prompt instead of being disconnected at death.

### Proposed code shape (follows `/develop:dotnet8`: rules in Domain, recipe-style orchestrator)

- `Domain.Game/Rules/DamageCalculator` and `SegmentHealth`: pure, static-free services, unit tested against the table in section 4.
- `Domain.Game/Services/RoomSimulation.Step(dt)`: orchestrator, one level of nesting, calls steps 1-8 above.
- `GameWorkerService`: only schedules timesteps and snapshot sends per room. No game logic.
- `IPlayer` gains `Mass`, `Segments` (hp per segment), `Boosting`; `Score` becomes derived (or kills + mass, decision needed).
- Tests first: damage table, heal targeting, order-independent head-to-head, projectile moved exactly once per step. `src/Tests` is empty today, so this adds the first test project.

## 7. Decisions (accepted defaults, 2026-10-17)

1. Segment break: remove that single segment, body closes up.
2. Head vs own body: ignored.
3. World edge: wrap-around.
4. Head vs head: both die.
5. Movement: free-angle simulation, pixel-art rendering on the client.
6. Leaderboard score: current mass.

## 8. Suggested implementation order

1. Test project + `DamageCalculator`/`SegmentHealth` (pure, quick, validates the formula). **Done**: `src/Domain/Domain.Game/Rules` (`DamageModelOptions`, `DamageCalculator`, `SegmentHealth`, `BodyHealer`), tests in `src/Tests/Domain.Game.Tests`, section 4 table asserted in tests.
2. `RoomSimulation` with fixed timestep, moving existing behaviors over and fixing findings 1-4, 7. **Done**: `Domain.Game/Simulation` (`FixedTimestepClock`, `RoomSimulation`), `GameWorkerService` now only schedules. Sim step = `ITickRateService.TickRate` (60 Hz today, movement math depends on it), snapshot every 2nd tick (30 Hz), max 5 catch-up steps. Dead players are reported by the step, notified, then removed by the worker. **Input queue done**: `GameHub` direction, shoot, join, rejoin and leave enqueue `IPlayerCommand`s into `IRoomInputQueue`; `RoomSimulation` drains at the start of each step (FIFO, cap 256 per drain). Join, rejoin and leave hub calls await the command result (3 s timeout). Rooms with only pending input are still processed so a first join works. Server polish since then: `OnDisconnectedAsync` now queues a `LeaveCommand` (not tied to the aborted connection token) and tells the room; direction and boost commands implement `ICoalescableCommand`, so several of them from one connection in the same drain collapse into the last; `GameHub` is no longer registered as a singleton (SignalR must create one per call, a shared instance mixed up `Context` between concurrent calls). Still open: hub reads in `GetConnectionData` still touch shared state from the hub thread.
3. Mass/length model, body collision, death drops. **Done**: mass is `IPlayer.Score` (pellets carry `IFood.Value`); length = `Path.Count` = initial + floor(mass / `MassPerSegment`), grows one segment per step and shrinks at once. `SnakeCollisionRules` kills a head touching any segment of another snake (head-on kills both, own body ignored, evaluated on a frozen picture after everyone moved). `DeathDropRules` drops `min(mass, 40)` pellets spread along the body with total value equal to the mass; the old 'shooter steals victim score' rule is gone and a shot that kills is consumed. (The tick-per-segment gap noted here is fixed in step 4.)
4. Shoot-vs-body damage, healing, boost. **Done**. Segment model: `IPlayer.Path` is now the real body, one point per segment ordered tail to head, spacing `HeadSize`. The head moves each tick and `SnakeBodyRules` pulls every follower along like a rope (only when farther than the spacing, distances measured the short way around the wrapping world). New segments appear stacked on the tail, one per tick, until length matches mass. `IPlayer.SegmentHealth` holds one HP ratio (0..1) per segment, aligned with `Path`; max HP comes from `DamageCalculator.MaxSegmentHp(length)`. A shot on the head kills; on the body it takes `ShotDamage(shooterLength, targetLength)` HP from the nearest-to-head segment it overlaps and is consumed. A segment at 0 HP breaks (removed, body closes up, one `MassPerSegment` of mass lost and dropped as a pellet) only while the snake is longer than its initial length; initial segments just stay at 0 HP (fully red) until healed. Eating a pellet heals `HealPerFruit * value` HP, most damaged segment first. Boost (`BoostRequest { Active }` on the hub, held) multiplies speed by 1.8 and burns 1 mass per 30 ticks (2/s at 60 Hz); it stops when mass is 0. Shot cooldown is done: 250 ms counted in ticks (`ShotCooldownRules`, 15 ticks at 60 Hz), ticked down in `MovePlayer`. Not done: `HeadSize` edge cases for collisions across the world wrap, and client rendering of the HP gradient (send `segmentHealth` ratios as `lerp(snakeColor, red, 1 - ratio)`).
5. Angle input, then client rendering of hp gradient, then eye tracking.

   **Angle steering (server) done**: `IPlayer.Heading` / `TargetHeading` in radians (0 = right, positive = clockwise on screen, up = -PI/2). Hub call `Steer { angle }` (coalescable per connection) sets the target and marks the direction `angular`; `MovePlayer` turns the heading toward it by at most `TurnRateDegreesPerSecond` (default 360, so 6 degrees per tick at 60 Hz) the short way round, and rebuilds `Direction.Axis` as the unit vector. Keyboard `ChangeDirection` still snaps instantly and sets heading and target together. Spawn now sets `CurrentDirection` and the heading from the spawn axis (before, `CurrentDirection` stayed at its default, so pressing that default direction was ignored). `ChangeDirection` with `angular` is rejected without side effects. Shots fly along the snake axis, so they can go at any angle; their end point is now the ray/room border intersection (`ShotRangeRules`) for every direction. The old up/down end check was inverted, so upward shots never expired. Snapshot JSON gains `heading` and `targetHeading` (pinned by `SnapshotContractTests`).
