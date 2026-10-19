# Stats and achievements (contract)

Signed-in players (see [ACCOUNTS.md](ACCOUNTS.md)) have stats kept over all their lives. Guests have none. Achievements are unlocked **only by the
game server and the manager**, from what the server saw: a client never says it earned something.

## Flow

1. The client joins with `Authorization: Bearer <device token>` on `POST /join` (optional). The manager checks it (`AccountService.Authenticate`):
   a token that is present but not valid is `401 { "error": "invalidToken" }`; a valid one puts `AccountId` and the display name (`Name#09291`, which
   replaces the nickname) in the `PlayerJoinPayload` sent through RabbitMQ. No token: a guest, as today.
2. The game server tags the player with the account id and counts what happens in each life (`LifeStats`).
3. When a life ends (death, leaving, disconnect) the game server sends the numbers to the manager, once, and the manager answers with the
   achievements that this unlocked. The game server then tells the player's connection (`AchievementsUnlocked`).
4. The client shows a toast, and the profile page shows all stats and achievements.

## Stats (`StatKeys`)

Counters (the manager adds): `games` (1 per life that began), `deaths`, `kills`, `foods` (foods actually eaten, not the credit), `prizes`, `bossKills`
(turret brought down by the last shot), `lightningHits` and `meteorHits` (times the snake lost mass to a strike), `playSeconds`.
Maxima (the manager keeps the highest): `bestScore` (peak mass of one life), `longestSnake` (peak number of segments), `bestKillsInLife`.

## Achievements (`Achievements.Defaults`)

An achievement is `{ id, icon, stat, threshold }`: unlocked when the stat is at least the threshold. Names and descriptions are in the client by id
(en and pt). The list is in `Library.Commons.Game.Domain/Entities/Achievements.cs`.

## HTTP

| Call | Auth | Body | Answer |
| --- | --- | --- | --- |
| `POST /accounts/{id}/stats` | api key (like the other game server calls) | `{ "counters": { "kills": 3 }, "maxima": { "bestScore": 120 } }` | `200 { "unlocked": ["first-blood"] }`, `404` unknown account |
| `GET /accounts/me/profile` | bearer | | `200 { account: { id, username, tag, display, createdAt }, stats: { kills: 3, ... all keys, 0 when none }, achievements: [ { id, unlockedAt } ] }`, `401 invalidToken` |
| `GET /achievements` | none | | `200 [ { id, icon, stat, threshold } ]` |

The stats call ignores keys it does not know (so a newer game server never breaks an older manager), refuses negative or absurd values (a counter of one
report is at most 1 000 000), and is **idempotent per report**: the body also carries `"reportId": "<guid>"` and a report already applied is acknowledged without
being applied again (`unlocked` empty).

## Game hub event

`AchievementsUnlocked` to the player's connection: `{ "ids": ["first-blood", "hunter"] }`.

## Storage

Stats and unlocks live in the manager next to the accounts, in a JSON file for now (`AccountsConfiguration:StatsPath`, memory only when empty), behind an
interface so Postgres can replace it.
