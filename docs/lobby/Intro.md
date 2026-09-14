# Lobby service

`services/lobby/` is a small **ASP.NET Core minimal API** (C#, .NET 8) that sits beside the game
servers. It:

- **runs rooms:** it starts one headless Godot game server per room, keeps the permanent main
  room alive, and closes idle ones;
- **lists rooms** for the main menu and resolves 5-letter join codes;
- **stores match history** in SQLite, as reported by each room's game server at match end, and
  computes the **season leaderboard** from it.

It never carries gameplay: players connect straight to a room's UDP port.

## Pages

- [HTTP API](api.md): every public endpoint, with request and response examples.
- [Rooms](rooms.md): the room lifecycle, limits, the main room, and how rooms report back.
- [Match history & leaderboard](match-history.md): storage, ranking rules, pagination.

## Configuration

Every setting is an environment variable (`services/lobby/LobbySettings.cs`). The defaults suit
local development; production values live in `deploy/systemd/the-room-lobby.service`.

| Variable | Default | Production | Meaning |
|---|---|---|---|
| `LOBBY_LISTEN` | `http://127.0.0.1:5310` | same | Listen address. Keep it on loopback; nginx proxies `/api/`. |
| `GODOT_BIN` | — (required) | `/opt/the-room/godot-app/…/Godot_v4.7.2-stable_mono_linux.x86_64` | Godot binary used to start rooms |
| `GAME_PATH` | — (required) | `/opt/the-room/app` | Project folder rooms run from (`--path`) |
| `LOBBY_DB` | `lobby.db` | `/opt/the-room/data/lobby.db` | SQLite file |
| `PUBLIC_HOST` | `127.0.0.1` | `room-udp.iscoded.com` | Hostname clients are told to connect to |
| `MAIN_ROOM_PORT` | `60010` | `60010` | Permanent room's UDP port (0 = no main room) |
| `ROOM_PORT_MIN` / `ROOM_PORT_MAX` | `60011` / `60030` | same | UDP ports for player-created rooms |
| `MAX_ROOMS` | `4` | `4` | Player-created rooms at once (each is a Godot server of about 210 MB) |
| `ROOM_IDLE_SECONDS` | `300` | `300` | Close a created room after this long with nobody in it |

## Running it

```bash
make run-lobby          # local lobby on :5310; rooms start from this checkout on ports 60410+
make run-client-menu    # the game's menu pointed at it (--api=http://127.0.0.1:5310)
make lobby-test         # xUnit tests (also part of make test)
```

In production it runs as the `the-room-lobby` systemd service. See [Deployment](../deployment.md) and
[Server configuration](../server-config.md).
