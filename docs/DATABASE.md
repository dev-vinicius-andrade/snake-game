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
| `unlocked_achievements` | which achievement, and when | account + achievement |

Tables and columns are snake_case. Times are stored as UTC (`timestamptz` refuses any other offset). The model uses plain types only (no
`jsonb`, no arrays), so the same model also runs on SQLite, which is what the store tests use.

What is **not** in it, as everywhere else: the recovery phrase, a device token, a social account's id. Only their hashes.

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

- Moving the accounts already in the JSON files into the database (there is no importer; today a manager that gets a connection string starts empty).
- Gameplay config, rooms and perks are still files / memory.
