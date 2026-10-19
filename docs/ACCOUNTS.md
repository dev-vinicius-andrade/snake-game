# Accounts (design and contract)

Goal: players can have a persistent identity, so they can earn achievements, **without an email or a password**. Guests keep playing as
they do today: a nickname, no tag, no achievements.

## Identity

- `username`: what people see. **Not unique.** 2 to 20 characters: letters (any language), digits, space, `_`, `-`, `.`; never `#`; trimmed, runs
  of spaces collapsed; no name from the blocklist.
- `tag`: 5 digits, `00000` to `99999`. Unique **per username** (case does not matter): `Vini#09291` and `vini#09291` are the same account.
- A player may ask for a tag. If it is free they get it, otherwise (or when they ask for none) a random free one. Only when all 100 000 are taken
  for a username is there an error.
- Display: `Name#09291`. Guests cannot use a nickname that ends in `#` and digits, so nobody can pose as an account.

## What proves who you are

1. **Device token.** A random 256 bit secret made at registration or login, kept by the browser (localStorage), sent as `Authorization: Bearer <token>`.
   Only its SHA-256 is stored. One per device; `logout` revokes the one used.
2. **Recovery phrase.** To sign in on a new device (or after clearing the browser): username + tag + phrase. It is a password in all but name, so:
   - random by default: 4 words from a list of 2048 (`amber-otter-nine-cable`, about 44 bits); or chosen by the player: at least 12 characters, not on a
     list of common passwords;
   - stored only as a slow salted hash (PBKDF2-SHA256, parameters kept in the hash string so they can be raised later);
   - guessing is slowed per account and per IP with exponential backoff (never a permanent lock, so a stranger cannot lock a player out).
3. The username and tag are for sharing and looking people up. **They are never a credential.**

## Storage

`IAccountStore` (`Accounts/AccountContracts.cs`). `FileAccountStore`: a JSON file, loaded into memory, written atomically (temp file, then rename).
Postgres later is a second implementation of the same interface with a unique index on `(lower(username), tag)`.

## HTTP contract (manager api)

All bodies are JSON, property names in camel case. Errors are `{ "error": "<code>" }`.

| Call | Body | Success | Errors |
| --- | --- | --- | --- |
| `POST /accounts` | `{ username, tag?: number, phrase?: string }` | `201 { username, tag, display, token, recoveryPhrase }` | `400 invalidUsername`, `400 invalidTag`, `400 weakPhrase`, `409 noTagsLeft`, `429 throttled` |
| `POST /accounts/login` | `{ username, tag, phrase }` | `200 { username, tag, display, token }` | `401 invalidCredentials`, `429 throttled` (+ `retryAfterSeconds`) |
| `GET /accounts/me` | header `Authorization: Bearer <token>` | `200 { id, username, tag, display, createdAt }` | `401 invalidToken` |
| `POST /accounts/logout` | bearer | `204` | `401 invalidToken` |
| `GET /accounts/availability?username=&tag=` | | `200 { available: boolean, username }` (`username` as it would be kept) | `400 invalidUsername` |

`recoveryPhrase` is the phrase to show the player **once** (the random one made for them, or the one they chose, echoed back so the screen can confirm it).
`invalidCredentials` is the same answer for an unknown account and a wrong phrase.

## Later

Stats counters and achievements (see the plan in the conversation): game servers send stat deltas for signed-in players, the manager keeps them next to the account.
