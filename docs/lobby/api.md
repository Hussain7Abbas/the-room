# Lobby HTTP API

Base URL in production: `https://room-api.iscoded.com`. Locally: `http://127.0.0.1:5310`.
JSON uses camelCase. Errors come back as `{"error": "human-readable message"}` with a 4xx/5xx
status, and the menu shows that message as-is.

The game's client for this API is `core/LobbyApi.cs`. Its records mirror
`services/lobby/Models.cs`, so change both together.

## Health

`GET /api/health` → `200 {"status":"ok"}`

## Rooms

### List public rooms

`GET /api/rooms` → `200` array, main room first, then fullest first.

```json
[
  { "code": "MAIN", "name": "The Room", "mode": "duelpit", "isPrivate": false,
    "players": 4, "maxPlayers": 10, "state": "playing",
    "host": "room-udp.iscoded.com", "port": 60010, "createdAt": "2026-09-14T15:04:17Z" }
]
```

`state` is one of `starting`, `waiting` (nobody in it), `playing`, `last call` or `results`. It
comes from the room's heartbeat every 5 s.

### Get a room by code (including private rooms)

`GET /api/rooms/{code}`: case-insensitive. Returns `200` with the room, or
`404 {"error":"No room with code K7QXP."}`.

### Create a room

`POST /api/rooms`

```json
{ "name": "Friday knives", "mode": "chaos", "isPrivate": true }
```

- `name`: optional, up to 24 characters. Control characters are stripped. Empty means
  "Duel Pit" or "Chaos room".
- `mode`: `duelpit` (10 players) or `chaos` (20 players).
- `isPrivate`: private rooms aren't listed and can only be joined by code.

Returns `201` with the room (its `state` is `starting` until the game server's first heartbeat,
about 1–2 s). Errors:

| Status | When |
|---|---|
| `400` | bad `mode` |
| `429` | more than 3 creates per minute from one IP |
| `503` | every room slot (`MAX_ROOMS`) or port is in use |
| `500` | the game server process failed to start |

To join, connect to `host:port` over UDP. The menu does that with `Net.Join`.

## Match history

### List matches (paginated)

`GET /api/matches?page=1&pageSize=10&player=Ana`

- `page` ≥ 1 (default 1); `pageSize` 1–50 (default 10). Out-of-range values are clamped.
- `player` (optional) returns only matches that player was in. Case-insensitive.

```json
{
  "items": [
    { "id": 42, "roomCode": "MAIN", "roomName": "The Room", "mode": "duelpit",
      "endedAt": "2026-09-14T15:30:01Z", "durationSeconds": 480,
      "mvpName": "Ana", "mvpScore": 25, "playerCount": 8,
      "topPlayers": [ { "rank": 1, "name": "Ana", "character": "blink", "score": 25, "kills": 19, "deaths": 6 } ] }
  ],
  "page": 1, "pageSize": 10, "totalItems": 137, "totalPages": 14
}
```

The newest match comes first. `topPlayers` holds up to 3 players. `mvpName` is `null` if nobody
scored.

### One match

`GET /api/matches/{id}` → the same fields, plus `players` (everyone, by rank) and `awards`
(strings such as `"SHARPEST REFLEXES: Ana (3 parries)"`). Returns `404` if the match isn't found.

## Leaderboard

`GET /api/leaderboard?page=1&pageSize=10`

```json
{
  "items": [ { "rank": 1, "name": "Ana", "matches": 12, "wins": 7, "kills": 140,
               "deaths": 61, "totalScore": 210, "bestScore": 25 } ],
  "page": 1, "pageSize": 10, "totalItems": 23, "totalPages": 3
}
```

Ranked by wins, then total score, then name. `GET /season` (no `/api`) returns the first 50 rows,
kept for older links.

## Internal routes (not public)

Used only by rooms' own game servers on the same machine (`core/RoomReporter.cs`).
**nginx doesn't proxy them**, and each call must carry the room's secret token.

| Route | Body |
|---|---|
| `POST /internal/rooms/{code}/heartbeat` | `{ "token", "players", "state" }` → `204`, or `401` on a bad token |
| `POST /internal/rooms/{code}/matches` | `{ "token", "durationSeconds", "players": [{ "name", "character", "score", "kills", "deaths" }], "awards": [] }` → `201 {"id"}` |
