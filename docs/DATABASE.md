# Database (Postgres, Entity Framework Core)

The manager keeps **accounts** and their **stats and achievements** in Postgres when it is given a connection string. Without one it works
as before: JSON files (or memory only), see `docs/ACCOUNTS.md`. Nothing else is in the database yet (gameplay config, rooms, perks stay as they are).

## Turning it on

| Setting | Meaning |
|---|---|
| `ConnectionStrings__Database` | The Npgsql connection string (`Host=...;Port=5432;Database=snake;Username=snake;Password=...`). Empty: no database. |
| `Database__MigrateOnStart` | `true` (default): the manager applies the pending migrations when it starts, before it serves anything. `false`: run them by hand or from a pipeline. |

On the home cluster: put `DATABASE_CONNECTION=...` in `deploy/homelab/.env`; `deploy.sh` keeps it in the Secret `snake-secrets` (key
`database-connection`) and the manager reads it from there. The Secret checksum restarts the manager when it changes.

**Which database user.** The manager should run as a user that can read and write rows but not change tables (a "write" user), and the
migrations should be applied by a user that owns them (an "admin" user). So on the home cluster `Database__MigrateOnStart` is `false` in
`deploy/homelab/values/manager.yaml`: apply the migrations with `dotnet ef database update` (below) before deploying a build that has a new one.
A manager that starts on an older schema fails on its first query, not at start.

If the migration fails, or the database cannot be reached, the manager does not start: running on a database of another shape is worse than
not running. A store never answers "no such account" for a database it could not reach: it throws.

## The model (`Persistence/`)

| Table | What | Unique |
|---|---|---|
| `accounts` | id, username (as typed), `username_key` (lower case), tag, `phrase_hash`, display name, created at | `username_key` + `tag` |
| `devices` | one signed in browser: the hash of its token, label, created at | the token hash, over all accounts |
| `external_logins` | a linked social account: provider, keyed hash of its id, linked at | provider + hash; one per provider and account |
| `stats_heads` | one row per account with stats; `version` is a concurrency token | account |
| `stat_values` | a counter total or a highest maximum, by key (`kind` 0 / 1) | account + kind + key |
| `applied_reports` | the report ids already applied, with a sequence; only the newest 200 per account | account + report |
| `roles` | the roles there are: `user` and `admin` (docs/ROLES.md) | name |
| `account_roles` | which role an account has; only grants are stored, `user` is implied | account + role |
| `achievements` | the catalog: id, icon, stat, threshold, sort order, `enabled`. Filled from the code when the manager starts (only the ones that are not there yet), then **changed from the admin page** (docs/ACHIEVEMENTS.md) | id |
| `achievement_texts` | the name and description an administrator wrote, by language. A built-in achievement has none: the site has its words | achievement + language |
| `unlocked_achievements` | which achievement, and when; points at `achievements` | account + achievement |

Tables and columns are snake_case. Times are stored as UTC (`timestamptz` refuses any other offset). The model uses plain types only (no
`jsonb`, no arrays), so the same model also runs on SQLite, which is what the store tests use.

### Gameplay config (what the admin page edits)

With a database, the config the game servers run on (perks, prizes, world events, the boss, room lifetimes) is kept in tables instead of
`gameplay.json`. The admin page and the game servers did not change: the api and the JSON they see are the same.

| Table | What |
|---|---|
| `gameplay_settings` | one row: the revision, the switches (perks, prizes, world events), the shot spread, burst interval, kill credit, and the room lifetimes |
| `perks` | id, name, foods required, enabled, icon, description, **script**, sort order |
| `perk_effects` | what a perk does, in order: effect name and amount |
| `perk_tiers` | the steps up of a perk: foods required and multiplier |
| `perk_rooms` | the rooms a perk is for, or (`is_excluded`) not for |
| `prizes` | the kinds of prize: timing, value, foods credit, colour, **the perk it gives** (a link to `perks`) |
| `prize_rooms` | the same for prizes |
| `world_event_lightning` | the lightning storms (one row) |
| `world_event_meteors` | the meteor rain (one row) |
| `world_event_zone` | the shrinking zone (one row) |
| `bosses` | the boss: today the turret (`id = turret`), with its hit points, volleys, reward and **the perk it gives** (a link to `perks`). A new kind of boss is a new row |
| `world_event_rooms` | the rooms an event or the boss is for, or not for |

- A save replaces every row in **one transaction**, so the tables always hold one whole config. `gameplay_settings.revision` changes with each
  save: game servers apply a revision they have not seen. The manager looks at the revision again every 5 seconds, so a change made on
  another manager, or by hand in the tables with a new revision, reaches it soon.
- A prize or the boss can only give a perk that is in `perks` (the database refuses anything else).
- The first time the manager runs on an **empty** database it fills the tables from the old `gameplay.json` when that file is there, else from the defaults.
- The built-in perks, which an empty list stands for, are written out as rows, so they can be edited like any other.

What is **not** in it, as everywhere else: the recovery phrase, a device token, a social account's id. Only their hashes.

## What goes in at start (the seed)

When the manager starts with a connection string, after the migrations (if switched on), it puts in the database what the game starts from. Every
step is safe to run again, and a failed step is logged and does not stop the manager.

| Step | What | When |
|---|---|---|
| Achievements | the achievements of the game, from the code | always, but only the ones not in the table yet: what is there is never overwritten, an administrator may have changed it |
| Gameplay config | the perks, prizes, events, boss and room lifetimes: the old `gameplay.json` if the file is there (`GameplayConfiguration__StorePath`), else the defaults (the built-in perks written out) | only when the gameplay tables are empty |
| Files | the accounts (`AccountsConfiguration__StorePath`) and their stats and unlocks (`AccountsConfiguration__StatsPath`) | only when `accounts` is **empty**, in one transaction: all of it or none |

So the data the game already has in its files is what the database starts with. The files are only read: they are not changed or deleted, and
they stay as the way back (remove the connection string and the manager uses them again). Once the database has accounts the files are ignored,
so anything players did after that is only in the database.

An unlock of an achievement that is no longer in the game is left out of the import (the catalog could not point to it); the log says how many.

On the home cluster the manager keeps the two paths set (`deploy/homelab/values/manager.yaml`), so the first start with a connection string imports
what is on the pod's volume.

## The stores

`EfAccountStore` and `EfStatsStore` implement `IAccountStore` and `IStatsStore`, the same interfaces the file stores do, so `AccountService`
and `AchievementService` did not change. They open a pooled `DbContext` per call.

- A username (any case) and tag, a device token hash and a social account each belong to one account. The **database** enforces it with unique
  indexes, so it holds with several manager instances too. A refused add or update changes nothing.
- A report is applied in **one transaction**: the report id, the counters and maxima and the unlocks together, or none. `stats_heads.version`
  is a concurrency token: if two reports for one account run at once, the one that loses starts again from what the winner saved, so the
  numbers add up exactly and an achievement unlocks once.

## Migrations

Migrations are in `src/Application/Application.Manager.Api/Persistence/Migrations`. The tool is a local tool (`dotnet-tools.json`):

```bash
dotnet tool restore
# add one, after changing the model (it never connects)
dotnet dotnet-ef migrations add WhatChanged --project src/Application/Application.Manager.Api --output-dir Persistence/Migrations
# apply them by hand (the connection string comes from SNAKE_DB)
SNAKE_DB="Host=localhost;Database=snake;Username=snake;Password=..." dotnet dotnet-ef database update --project src/Application/Application.Manager.Api
# the SQL instead of running it
dotnet dotnet-ef migrations script --idempotent --project src/Application/Application.Manager.Api
```

A test (`DatabaseWiringTests`) fails when the model has changes no migration covers, so a forgotten migration does not get through.
The history table is `migrations_history`.

## Tests

The store tests run against SQLite in memory, created from the model, with a connection per context like Postgres. They need nothing installed.
The migrations are checked against the model by the test above, but only a real Postgres runs them: do that once after a new migration
(`database update` above, on an empty database).

## Not done yet

- Rooms (the live registry) are still memory. Roles are in `docs/ROLES.md`.
