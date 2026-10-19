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

## Game server (as built)

- **Join.** `POST /join` (also `/join/room/{id}` and `/join/code/{code}`): a guest nickname that ends in `#` and digits is `400 { "error": "invalidNickname" }`
  (as the client's `isGuestNicknameAllowed`). The game server that accepts a signed-in player makes a one-time **join ticket** (random, for that room,
  2 minutes; `JoinTickets`) and puts it in the hub address it hands back: `gameServerUrl` is `https://…/game?joinTicket=<ticket>`. A client that connects
  to the address it was given sends it without knowing; `GameHub.JoinRoom` redeems it and the player plays as its account under its display name,
  whatever `nickname` it sends. No ticket, or one used, expired or for another room: a guest with its nickname.
- **Counting** (`Domain.Game/Stats/LifeStats`, only for players with an account). `foods`: one per item eaten, whatever it credits (a prize counts as a
  food and as a prize). `kills`: a snake that dies on your shot or runs into you. `bossKills`: the snake whose shot brings the turret down. `lightningHits`
  and `meteorHits`: a strike that cost mass, a head hit included; the zone counts for neither. Peaks of mass and segments are taken every move and at
  the end. `playSeconds`: whole seconds from the join to the end. Every value of a report is capped at 1 000 000.
- **Once per life.** The numbers are sent when the player is removed (`GameStateService.DisconnectPlayer`): after a death that is once `PlayerDied` was
  sent (`GameWorkerService`); for a leave, a disconnect or a closed room it is right then. A player is removed once, so its life is reported once.
- **Sending** (`HttpStatsReporter`, `StatsReportWorkerService`). Reporting only queues (1 000 at most; one more is dropped and logged), so the game loop
  never waits. `POST /accounts/{id}/stats` with the api key the game server already uses toward the manager. No answer, `5xx`, `408` or `429`: tried
  again after 1 s, 2 s, 4 s, 8 s with the same `reportId`, then dropped and logged. Another `4xx` (`404` unknown account): dropped at once.
- **Unlocks.** The answer's `unlocked` goes as `AchievementsUnlocked` to every hub connection of the account on that server: the one still open on the
  death screen, or the new one after playing again. None: dropped.

## Storage

Stats and unlocks live in the manager next to the accounts, in a JSON file for now (`AccountsConfiguration:StatsPath`, memory only when empty), behind an
interface so Postgres can replace it.
