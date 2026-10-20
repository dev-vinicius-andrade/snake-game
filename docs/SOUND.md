# Sound

The Vue UI (`src/Application/Application.Ui`) makes its sounds in the browser with the Web Audio API: pixel-art chiptune blips
(square, triangle and sawtooth tones, quick pitch slides, a little noise). There are no audio files, so nothing is downloaded or licensed.

## Pieces (`src/audio`, `src/composables/useGameSounds.ts`)

- `sounds.ts`: the recipe of every sound as a list of notes (start, length, wave, pitch, slide, gain). Add a sound by adding an id and a recipe.
- `soundEngine.ts`: the `SoundEngine` interface (`play`, `unlock`, `setVolume`, `setMuted`) and `WebAudioEngine`. The audio context is created
  only by `unlock()`, called from the first pointer, key or touch event (browser autoplay rules). The same sound is not restarted inside 60 ms,
  and master volume is capped so full volume is not painful. Without Web Audio it stays silent.
- `soundDirector.ts`: turns what changes between snapshots, plus damage and death notices, into `play` calls. It holds no audio, so specs use
  `FakeSoundEngine` (`src/test/fakeAudio.ts`). The first snapshot after a reset only seeds it, so joining a room is quiet.
- `soundSettings.ts`: mute and volume, saved in `localStorage` under `snake.sound` (every access in try/catch; blocked storage means defaults).
- `useGameSounds.ts`: wires the director to the game connection, listens for the first gesture, and exposes `muted`, `volume`, `toggleMute`, `setVolume`.

## Where it is heard

The `♪` button at the top centre of the game HUD mutes and unmutes. The volume slider is in the details panel (the `?` button). Texts are in
`game.sound` in `locales/messages/en.ts` and `pt.ts`.

| Sound | Trigger |
| --- | --- |
| `eat`, `prize`, `mazePrize` | own `foodsEaten` goes up; `prize` when a prize vanished next to the head; `mazePrize` when a maze stands |
| `shoot`, `boost` | a new shot of the own snake appears; own snake starts boosting |
| `hurt`, `broke` | `DamageDealt` against the own snake (`hit` or `lost`, or `broke`) |
| `hit`, `kill`, `turretHit` | own shot lands, kills, or lands on the boss (turret) |
| `die` | `PlayerDied` |
| `lightning`, `meteor` | a strike warning is gone from the newest snapshot |
| `mine` | a mine is gone from the newest snapshot |
| `bountyStart`, `bountyClaimed` | a bounty appears; it ends because the wanted snake died |
| `shower`, `rush` | fruit shower or sugar rush begins |
| `mazeOpen`, `mazeEnter`, `mazeWall` | a maze rises; the own head crosses into it; the own snake shrinks inside it without boosting |

## Limits

- Only the own snake's meals, shots and wounds are heard; other snakes are silent. Strikes, mines and the other world events are heard room-wide
  (no distance fade, no stereo).
- `mazeWall` is inferred from the own snake shrinking in the maze: the server sends no notice for a wall hit. A shot that breaks a segment
  inside the maze can trigger it too.
- Chiptune recipes were written by ear-less arithmetic and never listened to in a browser; tune pitches and gains in `sounds.ts`.
