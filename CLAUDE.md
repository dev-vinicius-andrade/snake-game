# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Multiplayer snake game. .NET 8 backend (SignalR + RabbitMQ + Redis) with an Angular 17 UI. Solution: `src/SnakeGame.sln`. All code lives under `src/`. No tests exist yet (`src/Tests` empty; roadmap item).

## Product goal and game rules

Goal: multiplayer snake game, pixel-art clone of slither.io. Unique feature: control snake with eyes (eye tracking) for accessibility.

Rules:
- Head hits another snake's body = dies. Dead snake's points/fruits drop around where it died; others can grab them.
- Eating food grows snake. Fruits eaten are also the resource for two abilities: **boost** and **shoot**. Both consume fruits, so snake shrinks again.
- Shot hitting a head kills. Shot hitting body does not kill: it breaks that body part and reduces size.
- Balance: big snake must not lose body to one shot. Damage = formula of shooter size vs target size (to design).
- Each body segment has hit points. Eating a fruit after being shot heals the most damaged segment.
- Damaged segments gradient to red as HP drops.

## Working conventions

- Always respond in caveman style (`/caveman:caveman`).
- .NET work: follow `/develop:dotnet8`. Site/frontend work: follow `/vue-skills-bundle:vue-best-practices`.
- Frontend target is the Vue app `src/Application/Application.Ui`. `Application.Ui.Angular` is a throwaway starter: ignore it, do not build on it (it holds uncommitted webgazer experiments; leave alone).
- Bootstrap subagents via `/savvy-flow:savvy-flow`. Model choice: cheap model (haiku / savvy-light) for simple tasks, sonnet for general coding, opus for heavy thinking or orchestration.

## Commands

Run from `src/` unless noted.

```bash
# Game server / manager API (.NET)
dotnet build SnakeGame.sln
dotnet run --project Application/Application.Game.Server/Application.Game.Server.csproj
dotnet run --project Application/Application.Manager.Api/Application.Manager.Api.csproj

# Vue UI (Application/Application.Ui) — the real frontend; port 5143; manager api url via VITE_MANAGER_URL (see .env.example)
npm install && npm run serve
npm run build       # vite build + vue-tsc
npm run lint
npm test            # vitest run (specs live next to sources as *.spec.ts)
npx vitest run src/game/snapshot.spec.ts   # single spec

# Angular UI (Application/Application.Ui.Angular) — legacy starter, ignore

# Run everything locally without compose: see docs/LOCAL_DEV.md (RabbitMQ recipe, env vars, join flow)

# Docker compose (profiles pick which services start)
docker compose --profile server-core-plus-local-enviroment-plus-ui-angular up --build
```

Compose profiles: `server-core`, `server-core-plus-local-enviroment` (adds redis, rabbitmq, etc.), `server-core-plus-ui-angular`, `server-core-plus-local-enviroment-plus-ui-angular`, `ui-angular`, `local-enviroment`. Local HTTPS dev certs: README says use Visual Studio with `docker-compose.dcproj` as startup project.

## Architecture

Three cooperating apps (diagram: `docs/images/architecture.png`):

- **Application.Game.Server** — owns game state per room (snakes, food, collisions). Many instances can run. Background workers in `Workers/`: `EventbusWorkerService` (consumes RabbitMQ `player-join-request` queue), `GameWorkerService` (game loop), `FoodGeneratorWorkerService`. `Hubs/GameHub.cs` is the SignalR hub players connect to for gameplay.
- **Application.Manager.Api** — middleware between UI and game servers. `Controllers/JoinController` receives join requests and publishes to RabbitMQ; `Hubs/ManagerHub` pushes the chosen game server's connection data back to the UI over SignalR. Auth via API keys (`AuthConfiguration:ApiKeys`).
- **Application.Ui** (Vue) — intended client; talks to Manager Api (REST + SignalR) then connects to assigned game server's SignalR hub. (Existing Angular UI follows same protocol but is legacy.)

Join flow: UI → Manager Api → RabbitMQ exchange → any Game Server with free room accepts (else requeues for another server) → server calls Manager Api (using `ManagementApiConfiguration:ApiKey`) → Manager notifies UI via SignalR with server address → UI connects to game server.

`Application.Service.Orchestrator` (Docker.DotNet worker, spawns game server containers) is unfinished — roadmap item.

Shared code in `src/Library/` (`Commons`, `Commons.Api`, `Commons.Eventbus.RabbitMq`, `Commons.Game.Domain`, `Commons.Game.Server`, `Extensions.DependencyInjection`) and `src/Domain/Domain.Game` (game logic). Each .NET app's `Program.cs` follows the same pattern: `InitializeAppConfiguration` loading from a `Configurations/` folder, `AddAppSettings<AppSettings>`, then DI/pipeline setup.

## Configuration

Each .NET app reads `Configurations/appsettings.json`; override with standard .NET env vars (`AppSettings__EventbusConfiguration__ConnectionString`, `ServerConfiguration__Domain`, ...). Detailed reference: `docs/configurations/*.md`. API key is committed in appsettings for demo purposes.

Angular uses `angular-server-side-configuration` (ngssc) for runtime env config in Docker; see `docs/configurations/ANGULAR_UI_CONFIGURATIONS.md`.

CI: `.github/workflows/docker-build-and-push-*.yml` build/push one image per app (angular, manager-api, game-server).
