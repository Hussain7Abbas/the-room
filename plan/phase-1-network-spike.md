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
- [x] Capsule player with predicted movement + reconciliation (`player/Player.cs`). Three roles: server runs the one authoritative sim from received input, the owning client predicts locally + sends input, every other client only interpolates (no local physics at all).
  - Reconciliation is **simplified** for this spike: on mismatch it either hard-snaps (`ReconciliationSnapDistance`, default 1.5m) or blends the live body toward corrected position over `ReconciliationSmoothTime` (default 0.12s) — not a full input-replay resimulation. Cheaper to build, can visibly correct on rough connections; worth revisiting with real playtest numbers before Phase 2 melee lands on top of it.
- [x] Snapshot/interpolation for remote players (`Player.InterpolateRemote`): proper delayed-buffer interpolation using `Tuning.InterpolationDelaySeconds` (default 100ms), not just exponential smoothing.
- [x] Per-tick hitbox history buffer + rewind hit test (`core/CombatServer.cs`): server-side ring buffer per player, pruned to `Tuning.MaxRewindTimeSeconds` (200ms). Single "stab" verb, fixed damage (`Tuning.StabDamage`/`StabRange`/`StabHitRadius`/`StabCooldown` — all temporary, replaced by real melee in Phase 2). Favor-the-shooter: attacker stays at current position, victims are rewound.
  - Rewind amount is driven by measured RTT/2 per peer (`core/PingService.cs`), not a client-supplied timestamp — simpler, and avoids trusting a client-reported clock.
- [x] Debug overlay (`core/DebugOverlay.cs` + `.tscn`): role, tick, ping, predicted/confirmed stab counts, last rewind ms, and the active `--sim-latency`/`--sim-loss` values when set. Always-on CanvasLayer, no toggle key yet.
- [x] Network condition simulator: `--sim-latency=<ms>` / `--sim-loss=<0..1>` CLI flags (`core/Net.cs`) delay/drop a client's own outgoing RPCs locally — lighter than true packet shaping, but enough to exercise reconciliation under bad conditions without needing a real bad connection.
- [x] Bot clients: `--bot` flag (`core/Net.cs`) + wander/stab AI in `Player.cs`. `make run-bots N=6` connects a swarm to a running server.
- [ ] **Deploy server to VPS — blocked on you.** No VPS credentials/access available here. `make deploy-server` is not yet scripted; the export-server target is also still a stub (needs `export_presets.cfg`, not created — see Phase 0). Tell me the VPS host/SSH access (or let me know if you'd rather set up the box yourself and just hand me an IP) and I'll finish both.
- [ ] **Playtest on real connections, 8 then 20 — blocked on the above.** Everything below was measured on localhost only.

## Bugs found & fixed while testing this phase
Both were real, silent correctness bugs — not caught by compiling or by Phase 0's brief smoke test — surfaced only by actually running server + multiple bot clients together and watching what happened:
1. **Multiplayer authority was never set on clients.** `Main.cs` called `SetMultiplayerAuthority()` only in the server's own spawn code; `MultiplayerSpawner` replicates node *creation*, not the authority flag, so every client saw its *own* player node as server-owned and silently never predicted, sent input, or ran bot AI. Fixed by setting authority (derived from the node's `Name`, which is the peer id) in `Player._Ready()`, which runs identically on every peer. Caught because bots produced zero stab attempts and zero movement input packets — worth remembering as a general Godot multiplayer gotcha.
2. **Grey-box CSG collision was flaky.** `CSGBox3D`/`CSGCylinder3D` default `use_collision` to `false` — nothing in the room collided at all initially (Phase 0 never actually ran a session long enough to notice players falling forever). After turning it on, most players landed fine but some free-fell through the floor indefinitely and never recovered, an intermittent issue under Jolt physics not fully root-caused here. Mitigated with a permanent "void catch" (`Tuning.VoidCatchY`, `Player.cs`): anyone below Y=-20 anywhere is reset to a spawn point — a reasonable feature for any map regardless of cause, but the underlying flakiness should get a closer look if it recurs once real melee (Phase 2) depends on precise collision.

## Measure (localhost only — see "blocked on you" above for real-network numbers)
- 6 headless bots + 1 headless server, 45s run: 6/6 peers connected, 5 confirmed stab hits, 74 correctly-rejected "no target in range" attempts, **zero errors**, no crashes, no permanent falls (void catch verified working).
- Rewind amounts on localhost: 0–11ms (as expected — real numbers need the VPS test).
- Not yet measured: server tick time at 20 players, bandwidth per client, predicted-vs-confirmed mismatch rate under induced latency/loss (the `--sim-latency`/`--sim-loss` flags exist for this but haven't been run through a structured test yet).

## Exit
Not yet — go/no-go needs the real-connection playtest above. Chosen tick rate (30 Hz) and interpolation delay (100ms) are recorded in `tuning/tuning.tres` as defaults, tunable without a rebuild.
