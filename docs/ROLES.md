# Roles (who can manage the game)

An account has roles. Two exist:

| Role | Can |
|---|---|
| `user` | play. Every account has it, it is never stored. |
| `admin` | everything a user can, and use the **admin page**: the gameplay config (perks, prizes, world events, the boss), the rooms, and who else is an admin. |

The role belongs to the **account**, so it follows the player to any device they sign in on. It is looked at on **every admin request**, so taking
it away works at once, even in a browser that is still signed in.

## Getting in

The admin api (`/admin/...`) lets a request through when it comes from:

1. a player who is signed in (`Authorization: Bearer <device token>`, which the site already sends) **and has the `admin` role**, or
2. a request with one of the configured **admin keys** in `X-Admin-Key` (`AuthConfiguration:AdminApiKeys`). The key is how you get in before anyone has
   the role, and for scripts. A key that is sent and is wrong is refused whatever else comes with the request.

Signed in without the role is `403`; no key and not signed in, or an unknown token, is `401`.

## Making the first admin

The role needs the database (docs/DATABASE.md); without one, roles are kept in memory and are gone at restart, so use the key.

With the admin key, naming the player as they sign in, `Name#09291` (name, then tag):

```bash
curl -X PUT -H "X-Admin-Key: $ADMIN_KEY" https://snake.web.games.andradev.io/api/admin/roles/admin/Vini/9291
```

Or in the database, as a user that can write:

```sql
INSERT INTO account_roles (account_id, role)
SELECT id, 'admin' FROM accounts WHERE username_key = 'vini' AND tag = 9291;
```

After that the player opens the admin page signed in, with no key.

## The api

| | |
|---|---|
| `GET /accounts/me` | answers with `roles`: `["user"]`, or `["user", "admin"]` |
| `GET /admin/roles` | the roles there are, and the admins (by the name other players see, and by `Name#tag`) |
| `PUT /admin/roles/{role}/{username}/{tag}` | gives the role. `204`; `404 unknownAccount`; `400 unknownRole` |
| `DELETE /admin/roles/{role}/{username}/{tag}` | takes it away. `204`; `404 roleNotHeld` or `unknownAccount`; `409 cannotRevokeSelf` |

An admin cannot take the admin role from themself, so the game is not left without one by accident (the admin key can still).

## Tables

`roles` (`user`, `admin`, made by the migration) and `account_roles` (account, role; both linked, so an account that is deleted loses its roles).
