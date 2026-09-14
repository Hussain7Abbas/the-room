# THE ROOM — Build Plan (Godot)

Source docs: `GDD.md` v0.1, `DESIGN-PILLARS.md`, `CHARACTER-SPEC.md`.
Engine: **Godot 4.7.2 (mono build)**, Forward+, Jolt Physics, 3D.

> The GDD was written for Unity. This plan maps it to Godot: ScriptableObject → custom `Resource` (`.tres`),
> Unity headless server → Godot `--headless` dedicated server, NGO/Mirror → Godot high-level multiplayer (ENet).
> The milestone order (M0 → M4) from GDD §11 is kept exactly — phases do not start until the previous gate passes.

## Status legend
`[ ]` not started · `[~]` in progress · `[x]` done · `[!]` blocked

## Phases

| # | Phase | GDD milestone | Gate question | Status | Detail |
|---|---|---|---|---|---|
| 0 | Project foundation | — | Can anyone clone, open, and run a server + 2 clients locally in one command? | [~] | [phase-0-foundation.md](phase-0-foundation.md) |
| 1 | Network spike | M0 | Can we register a knife hit fairly at 8–20 players on real connections? | [~] | [phase-1-network-spike.md](phase-1-network-spike.md) |
| 2 | Grey-box combat | M1 | Is the light/heavy/parry duel fun with zero art and zero abilities? | [~] | [phase-2-greybox-combat.md](phase-2-greybox-combat.md) |
| 3 | Ability framework + 2 abilities | M2 | Does the ability grammar survive two independent designers? | [~] | [phase-3-abilities.md](phase-3-abilities.md) |
| 4 | Match shape | M3 | Does a match have a shape and an ending? (Bounty, Golden Knife, Last Call) | [ ] | [phase-4-match-shape.md](phase-4-match-shape.md) |
| 5 | Roster, art pass, awards | M4 | Does the roster read at a glance in a crowd? | [ ] | [phase-5-roster-awards.md](phase-5-roster-awards.md) |
| 6 | Meta & social layer | post-M4 | Do people rematch and talk about it afterwards? | [ ] | [phase-6-meta-social.md](phase-6-meta-social.md) |

## Cross-cutting rules (apply to every phase)
- **Pillar priority 2 → 4 → 3 → 1.** Readability beats pace beats identity beats comedy.
- **One tuning file.** Every number (damage, wind-up, cooldown, radius, duration) lives in `res://tuning/tuning.tres`. No magic numbers in gameplay code. Changed only between playtests.
- **Server-authoritative.** Clients predict movement only. All hits, damage, cooldowns, scoring resolve on the server.
- **Grey-box before art.** No character art until that character's ability passes the 7-question review gate.
- **Weekly playtest** from the end of Phase 1 onward, 45 min, same slot.

## Decisions (confirmed 2026-09-14)
| # | Decision | Confirmed | Why it matters |
|---|---|---|---|
| D1 | **Language** | **C#**. `.NET SDK 10.0.401` found at `/usr/local/share/dotnet` (not on shell PATH — add it, or the Makefile does it per-invocation). Godot mono build is 4.7.2. | Godot.NET.Sdk pinned to `4.7.2` in the `.csproj`; TFM `net8.0` (the runtime Godot 4.7 mono embeds). |
| D2 | **3D, third-person camera** | Confirmed | Over-the-shoulder / third-person rig, not top-down. Camera collision + readability at 20 players still has to be tuned in Phase 2. |
| D3 | **Ragdolls** | Build **procedural primitive ragdolls** (capsule/sphere `PhysicalBone3D` rig via Jolt) instead of a downloaded asset for grey-box (Phase 2). Revisit a real skinned-mesh ragdoll only at the Phase 5 art pass, and only from a clearly-licensed free asset the user picks — not auto-downloaded. | Avoids pulling in unvetted third-party assets/licenses sight-unseen; grey-box phase shouldn't depend on art anyway. |
| D4 | **Networking stack** | Godot high-level multiplayer over **ENet**, dedicated headless server on a **VPS** | Resolves the GDD §12 Steam question: free, self-hosted. |
| D5 | **Jump / hop** | Build no-jump first; A/B a low hop in Phase 2 | GDD §5.1 leaves it `TBD`. |
| D6 | Git | Done — repo already initialized by the user, `origin` = `github.com/Hussain7Abbas/the-room`, one commit (`init`). | Tuning file must be version-controlled per GDD §9. |

## Change log
- 2026-09-14 — v0.1 plan created.
- 2026-09-14 — Decisions D1–D6 confirmed (C#, 3D third-person, procedural ragdolls for grey-box, VPS/ENet, no-jump-first, git already done). Starting Phase 0 implementation.
- 2026-09-14 — Phase 0 mostly built: C# project (`The Room.sln`/`.csproj`, Godot.NET.Sdk 4.7.2, net8.0), `tuning.tres` + `Tuning.cs` with all GDD §5 numbers, `Net`/`Events`/`TuningService` autoloads, CLI-driven server/client/offline boot (verified with a real 2-process ENet handshake), grey-box `Room.tscn`, `Player.tscn`/`.cs` (movement + 3rd-person camera, no combat yet), `Main.tscn` spawning players via `MultiplayerSpawner`, input map, Makefile (`make run-local` etc., verified). **Not done:** test framework (gdUnit4/GoDotTest — deferred to Phase 1, needs more setup than fits here) and export presets (`export-server`/`export-client` are stubs). Committed (`a6742cc`) and pushed to `origin/main`.
- 2026-09-14 — Phase 1 mostly built: three-role movement (server-authoritative sim / client prediction+reconciliation / remote interpolation) in `player/Player.cs`, server-side hitbox history + rewind lag comp for a temporary "stab" verb (`core/CombatServer.cs`), RTT measurement (`core/PingService.cs`), debug overlay, `--bot` wander/stab AI + `make run-bots`, `--sim-latency`/`--sim-loss` local network simulation. **Found and fixed two real bugs by actually running it**: multiplayer authority never propagated to clients (nothing was predicting), and grey-box CSG collision was flaky under Jolt (players fell through the floor) — mitigated with a permanent void-catch respawn. Verified stable on localhost (6 bots, 45s, 5 confirmed hits, 0 errors).
- 2026-09-14 — VPS deploy done: user pointed at `ssh kios-chat`, a *live shared production* box (existing nginx/Next.js/Postgres/Redis). Deployed the game server fully isolated — dedicated system user `theroom`, own directory, own hardened systemd unit — touching nothing existing and making no firewall/security changes (none were needed). Scripted as `make deploy-server`/`deploy-status`/`deploy-logs`. Ran a real WAN test: 4 headless bots connected from a different machine over the actual internet — 90–95ms RTT, 39–52ms rewind (well inside the 200ms cap), zero errors, clean disconnects, server stayed healthy. Phase 1 exit note: **provisional go** — architecture holds up over a real network hop; a true 8-then-20-human team playtest is still outstanding (only the team can do that part). Phase 1 → `[~]`, close to done.
- 2026-09-14 — User set up two Cloudflare subdomains: `room-api.iscoded.com` (proxied/orange-cloud, for the future Phase 6 HTTP API) and `room-udp.iscoded.com` (DNS-only/grey-cloud, for the actual game server — caught in time that Cloudflare's proxy can't carry raw UDP, so this had to be the un-proxied one). Set up `deploy/nginx/room-api.iscoded.com.conf` as a placeholder JSON endpoint with a real Let's Encrypt cert via certbot, matching this box's existing per-app nginx-config convention. Verified the game connects via `room-udp.iscoded.com:60010`. Repo docs (README, phase-1 doc) now point at the hostname instead of the raw IP.
- 2026-09-14 — Phase 2 mostly built: server-authoritative combat state machine (`player/Player.cs`) — light/heavy/parry/dash/execute, reusing Phase 1's rewind hit resolution (generalized in `core/CombatServer.cs`); real health/death/respawn/spawn-protection replacing the Phase 1 stopgap; a cosmetic primitive-ragdoll death effect; a killfeed + scoreboard UI (`core/KillfeedUI.cs`) driven by a new `Events.PlayerKilled` signal; a death cam; hop A/B scaffolding via `Tuning.HopEnabled`. **Found and fixed two more real bugs by actually running 6-bot duels repeatedly** (not by reading the code): a heavy kill was silently un-killing its victim back to "staggered" right after, and a ragdoll-spawn call set `GlobalPosition` on a node before it had a parent (root-caused via the full C# stack trace, not guessed). Verified every verb fires correctly including a deliberately-forced parry test — 18 kills across light/heavy/execute in one 90s/6-bot run, zero errors after both fixes. **Deferred, honestly**: audio/VFX hit tells and off-screen attacker indicator (need assets/UI work that don't belong bolted onto this pass), score target/timer/results-screen/rematch and Duel-Pit-vs-Chaos config selection (moved to Phase 4, where they overlap with "does a match have an ending" anyway), structured per-match telemetry (nothing worth logging yet without real human playtests), "respawn furthest from largest cluster" (still random). Phase 2 → `[~]`.
- 2026-09-14 — Phase 3 mostly built: `abilities/Ability.cs` base class enforces the CHARACTER-SPEC.md grammar in code (refuses to activate an ability whose tell or cooldown breaks the hard rules, not just at review time); `AbilityDef`/`CharacterDef` resources and `AbilityValidator` (runs every boot); two example abilities — Blink (Mobility) and Fire Patch (Zone Denial, with a reusable `AbilityZone` shared-kit helper) — **honestly labeled throughout as Claude-authored framework examples, not real character submissions** (Pillar 3 needs a real owner). Character select UI deferred in favor of a `--character=<id>` flag. **Found and fixed a real bug that predates this phase**: display names never actually reached other clients — `MultiplayerSpawner` replicates node creation, not property changes made after spawn, so every client's name label and the Phase 2 killfeed/scoreboard silently stayed blank for anyone but the server the whole time. Fixed with a proper identity-broadcast handshake. Verified via bots: names sync, an unknown `--character` falls back cleanly, and Fire Patch's damage-over-time was independently confirmed real (137 logged ticks at the exact expected per-tick amount, not just "the zone appeared"). Phase 3 → `[~]`.
