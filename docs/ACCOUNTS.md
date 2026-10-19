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

## Rules as built

Checked against `src/Application/Application.Manager.Api/Accounts/`.

- **Username cleaning** (`UsernameRules`): NFC, trim, every run of Unicode spaces (NBSP included) becomes one space. 2 to 20 characters, only
  letters, digits, space, `_`, `-`, `.`. Blocklist is a substring match, case-insensitive, on the name with spaces, `_`, `-`, `.` removed
  (`A d-m.i_n` is blocked as `admin`).
- **Tag parse** (`TagAllocator.TryParseTag`): `09291`, `9291` and `#09291` all mean tag 9291. 1 to 5 ASCII digits, optional leading `#`,
  surrounding spaces ignored. Stored and shown as 5 digits.
- **Recovery phrase normalization** (`RecoveryPhrase.Normalize`): NFKC, lowercase, every run of spaces and/or `-` becomes one `-`, none at the
  ends. Length of 12 or more is measured on the normalized form. Also weak: a common password, one repeated character, or the username
  (separators ignored for these three). Callers hash `Normalize(phrase)`, never the raw text.
- **Hasher** (`SecretHasher`): `pbkdf2-sha256$<iterations>$<salt base64>$<hash base64>`, 210000 iterations, 16 byte salt, 32 byte hash.
  `NeedsRehash` is true when the stored iterations are below the current setting, so the hash is replaced at next login.
- **Device token** (`DeviceTokens`): 32 random bytes, base64url without padding (43 characters). Stored as the SHA-256 of the token text, lowercase hex.
- **Throttle** (`AttemptThrottle`): 5 free failures, then waits of 2 s, 4 s, 8 s, ... doubling, capped at 15 min. A key with no failure for 1 h
  is forgotten. At most 100000 keys; the oldest 10% are dropped when full. A failure during a wait does not escalate.
- **File store** (`FileAccountStore`): JSON file `{ "version": 1, "accounts": [...] }`. Every change writes a temp file in the same folder,
  flushes it, then moves it over the file. A file that cannot be parsed is renamed to `<path>.corrupt-<utc time>` and the store starts empty.
  Every change rewrites the whole file.

## Known limits

- Look-alike letters (Cyrillic, fullwidth forms) still pass the username check, so `Vini` can be spelled with a Cyrillic `i`.
- Scripts with combining vowel marks (Hindi, Thai) are rejected by the letter check, because the marks are not letters.
- The blocklist is a substring match on the squashed name, so it has false positives (a name containing `retard` in a longer word, for example).
- No email recovery. Losing the browser and the recovery phrase loses the account.

## HTTP contract (manager api)

All bodies are JSON, property names in camel case. Errors are `{ "error": "<code>" }`.

| Call | Body | Success | Errors |
| --- | --- | --- | --- |
| `POST /accounts` | `{ username, tag?: number, phrase?: string }` | `201 { username, tag, display, token, recoveryPhrase }` | `400 invalidUsername`, `400 invalidTag`, `400 weakPhrase`, `409 noTagsLeft`, `429 throttled` (+ `retryAfterSeconds`) |
| `POST /accounts/login` | `{ username, tag, phrase }` | `200 { username, tag, display, token }` | `401 invalidCredentials`, `429 throttled` (+ `retryAfterSeconds`) |
| `GET /accounts/me` | header `Authorization: Bearer <token>` | `200 { id, username, tag, display, createdAt }` | `401 invalidToken` |
| `POST /accounts/logout` | bearer | `204` | `401 invalidToken` |
| `GET /accounts/availability?username=&tag=` | | `200 { available: boolean, username }` (`username` as it would be kept) | `400 invalidUsername`, `400 invalidTag`, `429 throttled` (+ `retryAfterSeconds`) |

`recoveryPhrase` is the phrase to show the player **once** (the random one made for them, or the one they chose, echoed back as sent so the
screen can confirm it). A blank `phrase` is the same as none: a random one is made.
`invalidCredentials` is the same answer for an unknown account and a wrong phrase (also for a missing or impossible username or tag), and it
costs the same time: an unknown account is checked against a dummy hash.
A `429` body is `{ "error": "throttled", "retryAfterSeconds": <whole seconds, rounded up> }` and the response has the same number in a
`Retry-After` header. Other error bodies have no `retryAfterSeconds`.
`tag` in the availability query is read like a typed tag (`09291`, `9291`, `#09291`); left out or blank, `available` says whether any tag is
still free for the name. A body that is not JSON of the right shape (for example `tag: "abc"`) gets the framework's `400` problem details, not
an `{ error }` body.

### Server side (`AccountService`, `AccountsController`)

- **Throttles** (`AccountThrottles`), three `AttemptThrottle`s with their own tuning; a key is checked first and a waiting key is refused
  without doing the work:
  - sign-in: keys `acct:<lowercase username>#<tag>` and `ip:<address>`. A wrong phrase or an unknown account counts a failure on both; the
    answer to a waiting caller is the longer of the two waits. Success forgets the account key, never the IP key. Tuning as in "Throttle" above.
  - registration: key `reg:<address>`. Every registration that passes validation counts (invalid input costs nothing and does not count):
    10 free, then 1 min doubling up to 15 min, forgotten after 1 h without one.
  - availability: key `avail:<address>`. Every check counts: 60 free, then 1 s doubling up to 1 min, forgotten after 10 min without one.
- **Client address**: `HttpContext.Connection.RemoteIpAddress` (IPv4-mapped IPv6 read as IPv4). `X-Forwarded-For` is not trusted: the app
  has no forwarded headers setup. Behind a reverse proxy every player would share the proxy's address, so add `UseForwardedHeaders` with
  the proxy listed as known before relying on the IP throttles there.
- **Devices**: at most 10 per account; signing in an 11th drops the oldest. Two sign-ins of the same account at once never lose each other's device.
- **Tag races**: when another registration takes the tag between the check and the add, the add is tried again with a new free tag, up to 5
  times, then `409 noTagsLeft`.
- **Rehash**: a login whose stored hash has fewer iterations than the current setting stores a new hash of the phrase.

### Configuration

`AccountsConfiguration:StorePath` (env `AccountsConfiguration__StorePath`): the accounts JSON file. Empty or missing: memory only, gone at restart.

## Later

Stats counters and achievements (see the plan in the conversation): game servers send stat deltas for signed-in players, the manager keeps them next to the account.
