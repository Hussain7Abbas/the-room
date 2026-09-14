# The Room

A fast, funny knife-fight deathmatch in one room, where every character is a caricature of the
developer who built them. See [`GDD.md`](GDD.md), [`DESIGN-PILLARS.md`](DESIGN-PILLARS.md), and
[`CHARACTER-SPEC.md`](CHARACTER-SPEC.md) for design.

Build plan and progress tracking: [`plan/main.md`](plan/main.md).

## Requirements

- **Godot 4.7.2 (.NET/mono build)** — this repo assumes `/Applications/Godot_mono.app` on macOS.
  Update the `GODOT` variable at the top of the `Makefile` if yours lives elsewhere.
- **.NET SDK 8+** (10.0.401 tested). On this machine it's installed at `/usr/local/share/dotnet`
  but not on the default shell `PATH` — the `Makefile` adds it automatically for every target,
  so you don't need to `export PATH` yourself.

## Quick start

```bash
make setup       # restore NuGet packages + sanity build
make run-local   # 1 local headless server + 2 windowed clients in the same room
```

Run `make help` for the full target list (server/client separately, custom port/player count, etc.).

## Networking

- `--server` — run headless as a dedicated server (also auto-detected when the display server
  is `headless`, e.g. an exported Linux server build — unless `--connect` is also given).
- `--connect=<host>` — join a server as a client. Defaults to `127.0.0.1`.
- `--port=<port>` — override the default port (`60010`).
- `--name=<name>` — display name sent to the server.
- No flags at all → runs fully **offline** (single local player, no networking) for quick solo
  iteration in the editor.

The current networking layer (`core/Net.cs`) is Phase 0 scaffolding only — plain ENet connect/host
with no prediction or lag compensation yet. That's the subject of
[`plan/phase-1-network-spike.md`](plan/phase-1-network-spike.md), the project's highest technical
risk per the GDD.

## Project layout

```
core/       # networking, tuning loader, signal bus, the Main scene/root script
player/     # player scene + controller (movement, third-person camera)
maps/room/  # the arena (grey-box)
tuning/     # tuning.tres — the single source of truth for every gameplay number
plan/       # phased build plan + status tracking
```

## The tuning file

Every gameplay number (damage, wind-ups, cooldowns, durations, radii) lives in
[`tuning/tuning.tres`](tuning/tuning.tres), backed by [`tuning/Tuning.cs`](tuning/Tuning.cs).
Character/ability owners control an ability's *shape* (code); this file controls its *strength*
(numbers) — see `CHARACTER-SPEC.md` Part 4. Don't hardcode gameplay numbers elsewhere.
