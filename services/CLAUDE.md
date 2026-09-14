# services/ — standalone .NET services

> **Keep this file in sync** with `services/`. When a route, setting, table or limit changes,
> update this file, `docs/lobby/` and (for deploy-relevant changes) `docs/server-config.md` in
> the same change.

`services/` is **excluded from the game build** (`The Room.csproj` `Compile Remove`) and from
Godot's scan (`services/.gdignore`). Each project builds on its own.

## lobby/ (ASP.NET minimal API, net8.0 with `RollForward=Major`)

- `Program.cs` holds the routes:
  - public `/api/*`: health, rooms, matches, leaderboard;
  - `/internal/rooms/{code}/*` (heartbeat, matches) for the rooms' own game servers.
    **nginx never proxies `/internal`**, and each room authenticates with its token.
- `RoomManager.cs` starts **one headless Godot process per room** with `ArgumentList` (never a
  shell string: room names are player input). It runs the permanent `MAIN` room, and caps
  created rooms with `MAX_ROOMS`. It closes empty rooms after `ROOM_IDLE_SECONDS` and
  rate-limits creates to 3 per IP per minute.
- `MatchStore.cs`: SQLite (`Microsoft.Data.Sqlite`, WAL).
  - The schema lives in `Migrate()` as `CREATE … IF NOT EXISTS`. Add columns with a guarded
    `ALTER TABLE`; never drop data.
  - Ranking: ties share a rank.
  - A match with a top score of 0 has no winner or MVP.
  - Paging is clamped to page ≥ 1 and size 1–50.
- `LobbySettings.cs`: every setting is an environment variable. The defaults are for local dev;
  production values live in `deploy/systemd/the-room-lobby.service`.
- `Models.cs` is mirrored by `core/LobbyApi.cs` in the game. **Change both together.**
- `Clean()` every string that comes from a player or a game server.

## Lobby.Tests/ (xUnit)

`make lobby-test` (part of `make test`). Every new rule in `MatchStore` or `RoomManager` needs a
test. Each test uses a throwaway SQLite file.
