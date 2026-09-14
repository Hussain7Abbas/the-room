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
| 1 | Network spike | M0 | Can we register a knife hit fairly at 8–20 players on real connections? | [ ] | [phase-1-network-spike.md](phase-1-network-spike.md) |
| 2 | Grey-box combat | M1 | Is the light/heavy/parry duel fun with zero art and zero abilities? | [ ] | [phase-2-greybox-combat.md](phase-2-greybox-combat.md) |
| 3 | Ability framework + 2 abilities | M2 | Does the ability grammar survive two independent designers? | [ ] | [phase-3-abilities.md](phase-3-abilities.md) |
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
- 2026-09-14 — Phase 0 mostly built: C# project (`The Room.sln`/`.csproj`, Godot.NET.Sdk 4.7.2, net8.0), `tuning.tres` + `Tuning.cs` with all GDD §5 numbers, `Net`/`Events`/`TuningService` autoloads, CLI-driven server/client/offline boot (verified with a real 2-process ENet handshake), grey-box `Room.tscn`, `Player.tscn`/`.cs` (movement + 3rd-person camera, no combat yet), `Main.tscn` spawning players via `MultiplayerSpawner`, input map, Makefile (`make run-local` etc., verified). **Not done:** test framework (gdUnit4/GoDotTest — deferred to Phase 1, needs more setup than fits here) and export presets (`export-server`/`export-client` are stubs). Nothing committed to git yet — working tree only.
