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
- [x] **Deploy server to VPS.** Deployed to `kios-chat` (SSH alias; port `60010/udp`), which is a live shared production box (nginx, Next.js apps, Postgres, Redis) — kept fully isolated: dedicated system user `theroom`, app lives at `/opt/the-room/app`, own systemd unit `the-room-server.service` (hardened: `NoNewPrivileges`, `ProtectSystem=strict`, scoped `ReadWritePaths`, auto-restart). No firewall/security-group changes were needed (`ufw` was already inactive) or made. `make deploy-server` (rsync + remote rebuild + restart), `make deploy-status`, `make deploy-logs` are scripted and working.
- [x] **Playtest on a real connection.** Not the full 8/20-*human* session yet (that needs an actual team session, not something to simulate solo) — but 4 headless bots connected from a different machine, over the real internet, to the deployed server, and stayed stable. See numbers below.

## Bugs found & fixed while testing this phase
Both were real, silent correctness bugs — not caught by compiling or by Phase 0's brief smoke test — surfaced only by actually running server + multiple bot clients together and watching what happened:
1. **Multiplayer authority was never set on clients.** `Main.cs` called `SetMultiplayerAuthority()` only in the server's own spawn code; `MultiplayerSpawner` replicates node *creation*, not the authority flag, so every client saw its *own* player node as server-owned and silently never predicted, sent input, or ran bot AI. Fixed by setting authority (derived from the node's `Name`, which is the peer id) in `Player._Ready()`, which runs identically on every peer. Caught because bots produced zero stab attempts and zero movement input packets — worth remembering as a general Godot multiplayer gotcha.
2. **Grey-box CSG collision was flaky.** `CSGBox3D`/`CSGCylinder3D` default `use_collision` to `false` — nothing in the room collided at all initially (Phase 0 never actually ran a session long enough to notice players falling forever). After turning it on, most players landed fine but some free-fell through the floor indefinitely and never recovered, an intermittent issue under Jolt physics not fully root-caused here. Mitigated with a permanent "void catch" (`Tuning.VoidCatchY`, `Player.cs`): anyone below Y=-20 anywhere is reset to a spawn point — a reasonable feature for any map regardless of cause, but the underlying flakiness should get a closer look if it recurs once real melee (Phase 2) depends on precise collision.

## Measure
**Localhost** (6 headless bots + 1 headless server, 45s): 6/6 peers connected, 5 confirmed stab hits, 74 correctly-rejected "no target in range" attempts, zero errors, no crashes, no permanent falls (void catch verified working). Rewind amounts 0–11ms.

**Real WAN** (4 headless bots run from a different machine on a residential/office connection, over the real internet, to the deployed VPS — not the same network at all): all 4 connected and stayed connected for the full run, clean disconnect on exit, server (`systemctl is-active`) stayed healthy throughout and after.
- **RTT: 90–95ms** per peer (first measurement, `PingService`).
- **Rewind amount: 39–52ms** (RTT/2, smoothed) — comfortably inside the 200ms cap (`Tuning.MaxRewindTimeSeconds`), so the rewind-clamp behavior wasn't even exercised at this latency; worth a deliberately-bad connection test (`--sim-latency=150` or a genuinely poor link) before fully trusting the cap.
- No confirmed hits this run (bots wandering independently, didn't happen to collide) but the request/response/rewind round-trip visibly worked end to end over the real link.
- Not yet measured: 8–20 *simultaneous* connections (only tested 4), server tick time under real load, bandwidth per client, predicted-vs-confirmed mismatch rate under induced loss (`--sim-loss` flag exists, not yet run through a structured test), and a genuine multi-human session (bots don't reproduce human input patterns or a real living-room's network).

## Exit
**Provisional go.** The architecture works over a real internet hop at small scale with no correctness issues (zero errors across all local + WAN runs) and the numbers so far (90ms RTT, <52ms rewind) sit well inside budget. Not a full go/no-go yet — that needs an actual 8-then-20-person team playtest, which only the team can do. Chosen tick rate (30 Hz) and interpolation delay (100ms) are recorded in `tuning/tuning.tres` as defaults, tunable without a rebuild.

## Deployed server
- Host: `kios-chat` (SSH alias), port `60010/udp`. Public IP is not recorded here (this repo is pushed to public GitHub) — get it from whoever manages the box, or `ssh kios-chat` and check locally.
- Isolated as user `theroom` under `/opt/the-room/app`, systemd unit `the-room-server.service`.
- Redeploy: `make deploy-server`. Check on it: `make deploy-status`, `make deploy-logs`.
- Known gap: relies on the box's cloud-provider network allowing inbound UDP `60010` — it evidently does (the WAN test connected), but that layer is outside SSH visibility/Claude's control if it ever needs to change.
