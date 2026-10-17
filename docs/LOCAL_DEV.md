# Running the game locally (without docker compose)

Verified on Windows 11 with Podman running inside a WSL2 distro (mirrored networking). Ports used: RabbitMQ 5672,
manager api 60510, game server 60520, Vue client 5143. The Vue client defaults to `http://localhost:60510` for the manager api
(override with `VITE_MANAGER_URL`, see `src/Application/Application.Ui/.env.example`).

## 1. RabbitMQ

With Docker or a normal Podman setup, `docker compose --profile local-enviroment up rabbitmq` is enough.

With Podman inside WSL2 (the setup this was tested on) `-p` is not applied for remote containers and the Erlang node needs a
resolvable name, so run it on the host network, as the `rabbitmq` user, with a fixed node name:

```bash
podman run -d --name snake-game-rabbitmq --network=host --user rabbitmq \
  -e RABBITMQ_ERLANG_COOKIE=snake-game-local -e RABBITMQ_NODENAME=rabbit@localhost \
  docker.io/library/rabbitmq:management
```

It takes about a minute before port 5672 answers on Windows `localhost`. Failures seen without those flags:
`.erlang.cookie: eacces` (run as `--user rabbitmq`) and `epmd error for host windows-11: timeout` (set the node name).

## 2. Manager api and game server

Build both once, then run with `--no-build`. Two parallel `dotnet run` builds lock the shared library projects and fail.

```bash
cd src
dotnet build Application/Application.Manager.Api/Application.Manager.Api.csproj
dotnet build Application/Application.Game.Server/Application.Game.Server.csproj

ASPNETCORE_URLS=http://localhost:60510 \
AppSettings__EventbusConfiguration__ConnectionString=amqp://localhost:5672 \
dotnet run --no-build --project Application/Application.Manager.Api --no-launch-profile

ASPNETCORE_URLS=http://localhost:60520 \
ServerConfiguration__Domain=localhost ServerConfiguration__Scheme=http ServerConfiguration__Port=60520 \
AppSettings__EventbusConfiguration__ConnectionString=amqp://localhost:5672 \
AppSettings__ManagementApiConfiguration__BaseUrl=http://localhost:60510 \
dotnet run --no-build --project Application/Application.Game.Server --no-launch-profile
```

Both apps print nothing and wait while RabbitMQ is unreachable. Redis is not needed for playing.

## 3. Client

```bash
cd src/Application/Application.Ui
npm install
npm run serve        # http://localhost:5143
```

## Join flow on the wire

1. The client connects to the manager hub (`/manager`) and posts `POST /join { id: <hub connection id>, nickname }`.
2. The manager publishes `PlayerJoinRequest` on RabbitMQ, a game server takes it, creates or picks a room and calls
   `POST /player/{id}/joined` on the manager.
3. The manager sends the hub event **`onJoinedRoom`** `{ gameServerUrl, roomId, nickname }` to that connection.
   (`onJoin` is only used by the api-key protected `POST /join/{id}`, which the game server does not call.)
4. The client connects to `gameServerUrl` (already ends in `/game`) and invokes `JoinRoom { roomId, nickname }`.

## What was checked live (2026-10-17)

- Join flow, snapshots at about 30 Hz, pixel rendering, food, leaderboard and score updates.
- Keyboard: `ChangeDirection` turns the snake instantly. Space: `Shoot` spends one mass and creates a shot with a correct end point.
- Mouse aim: `Steer` messages turn the snake at the server turn rate; the heading ends on the aimed angle.
- Two clients in one room, death (`PlayerDied`, overlay, pellets dropped, "Play again" rejoins).

Not checked live: HP gradient on a damaged body, boost, camera gaze (milestone 3).
