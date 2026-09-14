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
- `--bot` — run as a headless client driven by simple wander/stab AI instead of real input.
  `make run-bots N=6 HOST=<host>` connects a swarm to a running server.
- `--sim-latency=<ms>` / `--sim-loss=<0..1>` — artificially delay/drop this client's own outgoing
  RPCs, for testing prediction/reconciliation locally without a real bad connection.
- No flags at all → runs fully **offline** (single local player, no networking) for quick solo
  iteration in the editor.

Movement is server-authoritative with client-side prediction + reconciliation; the temporary
"stab" verb (real melee lands in Phase 2) uses server-side rewind lag compensation. See
[`plan/phase-1-network-spike.md`](plan/phase-1-network-spike.md) for the architecture, the two
real bugs found while building it, and real-WAN test numbers.

### Deployed server

A dedicated server is live at **`room-udp.iscoded.com:60010`** — connect with
`make run-client HOST=room-udp.iscoded.com` or `godot --path . -- --connect=room-udp.iscoded.com`.
It's DNS-only (Cloudflare grey-cloud): don't proxy this hostname, raw UDP can't cross Cloudflare's
HTTP-only proxy. Redeploy with `make deploy-server`; check on it with `make deploy-status` /
`make deploy-logs`. It runs isolated (own system user, own systemd unit) on a box shared with
other apps — see `plan/phase-1-network-spike.md` for details.

`room-api.iscoded.com` is a separate, unrelated placeholder (Cloudflare-proxied, HTTPS) for the
future HTTP API from Phase 6 — see `deploy/nginx/room-api.iscoded.com.conf`.

## Project layout

```
core/          # networking, combat/rewind, ping, tuning loader, signal bus, debug overlay, Main scene
player/        # player scene + controller (movement, camera, prediction, stab verb, bot AI)
maps/room/     # the arena (grey-box)
tuning/        # tuning.tres — the single source of truth for every gameplay number
deploy/nginx/  # nginx configs for HTTP-facing subdomains (not the game server itself — that's raw UDP)
plan/          # phased build plan + status tracking
```

## The tuning file

Every gameplay number (damage, wind-ups, cooldowns, durations, radii) lives in
[`tuning/tuning.tres`](tuning/tuning.tres), backed by [`tuning/Tuning.cs`](tuning/Tuning.cs).
Character/ability owners control an ability's *shape* (code); this file controls its *strength*
(numbers) — see `CHARACTER-SPEC.md` Part 4. Don't hardcode gameplay numbers elsewhere.
