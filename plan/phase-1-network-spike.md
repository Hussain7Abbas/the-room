# Phase 1 — Network Spike (GDD M0)

**Goal:** answer the project's highest technical risk before anything else is built.

**Gate question:** *Can we register a knife hit fairly at 8–20 players across Baghdad-quality internet?* If **no**, the design changes (smaller lobbies, wider hitboxes, slower wind-ups) before Phase 2.

## Architecture
- **Dedicated headless server** (Godot `--headless`, Linux export) on a VPS. Clients never host.
- **Transport:** `ENetMultiplayerPeer`, fixed server tick (target 30 Hz, tunable).
- **Movement:** client-side prediction + server reconciliation; other players rendered with ~100ms interpolation buffer.
- **Hits:** client sends *intent* (`attack(verb, aim_dir, client_tick)`), server resolves. Lag compensation = server keeps a ring buffer of every player's hitbox transform per tick and **rewinds up to 200ms max** (GDD §4) to the attacker's view time.
- **Feedback budget:** local hit spark/sound fires on input-predicted contact (<100ms), confirmed or cancelled by server.
- Hand-rolled RPC + state snapshots preferred over `MultiplayerSynchronizer` for players (we need tick-stamped history); synchronizer is fine for props.

## Tasks
- [ ] Capsule player with predicted movement + reconciliation.
- [ ] Snapshot/interpolation for remote players.
- [ ] Per-tick hitbox history buffer + rewind hit test (single "stab" verb only, fixed damage).
- [ ] Debug overlay: ping, tick, rewind amount used, predicted vs confirmed hits, packet loss.
- [ ] Network condition simulator (artificial latency/jitter/loss) for local testing.
- [ ] Bot clients: headless clients that wander and stab, to reach 20 players with few humans.
- [ ] Deploy server to VPS; script it (`make deploy-server`).
- [ ] **Playtest on real connections**, 8 then 20 (bots fill).

## Measure
- Server tick time at 20 players.
- Bandwidth per client.
- % of "I already walked away" complaints (should trend to zero with rewind cap).
- Predicted-hit vs server-confirmed mismatch rate.

## Exit
Written go / no-go note in this file with numbers. Record the chosen tick rate and interpolation delay in `tuning.tres`.
