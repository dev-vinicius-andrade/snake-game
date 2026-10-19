# Private rooms, invites and room administration

## Player flow

1. **Create room** (start page): optional name, optional max players. The manager picks a 6 character invite code
   (letters and digits without 0, O, 1, I, L) and asks a game server for a *private* room.
2. The host lands in the game with `?room=<id>&code=<code>`. A badge shows the code and copies the invite link
   `https://<site>/join/<code>`.
3. A friend opens the link (or types the code on the start page). The join page shows the room name and player count,
   asks for a nickname and joins through `POST /join/code/{code}`.
4. Private rooms are never picked by **Join random room**. Public rooms behave as before.

## Wire

| Step | Call |
| --- | --- |
| Host asks for a room | `POST /rooms { id: <manager hub connection id>, name?, maxPlayers }` (202, `{ code }`) |
| Manager to game servers | RabbitMQ `room-create-request` (`RoomCreatePayload`), first server with capacity takes it |
| Game server answers | `POST /rooms/{requesterId}/created` or `/failed` (API key), manager hub event `onRoomCreated` / `onRoomCreateFailed` |
| Invite preview | `GET /rooms/code/{code}` (404 when unknown) |
| Join by code | `POST /join/code/{code} { id, nickname }`, then the usual `onJoinedRoom` |
| Join refused | game server calls `POST /player/{id}/join-failed`, manager hub event `onJoinFailed { reason }` |
| Room registry | game servers send `POST /rooms/heartbeat` (API key) every 5 s with all their rooms; the manager forgets rooms of a server silent for 20 s |
| Admin close | manager calls `DELETE {server}/rooms/{id}` (API key); players get the hub event `RoomClosed` |

Room joins by id (`POST /join/room/{id}`) answer 404 when the registry does not know the room.
Join requests for a room another server owns are requeued until the owner takes them.

## Administration

- Page: `/admin` in the Vue app. Sign in with an admin key (kept in `sessionStorage`, gone when the tab closes).
  Lists all rooms (name, code, players, age, game server) and deletes them. Refreshes every 5 s.
- API: `GET /admin/rooms`, `DELETE /admin/rooms/{id}`, header `X-Admin-Key`.
- Keys: `AuthConfiguration:AdminApiKeys` of the manager. **Empty by default, which closes the admin API to everyone.**
  Set it with an environment variable, never commit a real key:

```bash
AuthConfiguration__AdminApiKeys__0=<long random value> dotnet run --no-build --project Application/Application.Manager.Api --no-launch-profile
```

- Deleting a room tells its players, removes them through the room's own queue, and drops the room and its game state.
  If the game server cannot be reached the room stays listed (502) and disappears once its heartbeats stop.

## Cleaning up

A game server closes **private** rooms that stayed empty for 10 minutes (sweep every 30 s, `EmptyRoomReaper`). The admin page (Gameplay, **Private rooms**) changes this while the game runs: minutes empty, and a longest life for any private room (0: no limit); the config key is `rooms` (`emptyPrivateRoomMinutes` 1..10080, `maxPrivateRoomMinutes` 0..10080). A room nobody ever joined counts from its creation; a room that emptied counts from the sweep that noticed it. Public rooms are never closed this way.

## Limits and known gaps

- The registry is in memory in one manager instance. A restart forgets rooms until the next heartbeat (5 s); private room codes come back with it.
- Game servers accept the manager's call with the same API key they use toward the manager.
- Observed once during a manual run: the first `JoinRoom` into a room whose host had died timed out after 3 s and worked on retry. Not investigated.
