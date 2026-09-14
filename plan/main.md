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
| 0 | Project foundation | — | Can anyone clone, open, and run a server + 2 clients locally in one command? | [ ] | [phase-0-foundation.md](phase-0-foundation.md) |
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

## Decisions needed before Phase 0 starts
| # | Decision | Recommendation | Why it matters |
|---|---|---|---|
| D1 | **Language: GDScript or C#?** | **GDScript** | Godot mono is installed but the .NET SDK is **not** (`dotnet` not found). GDScript has zero setup, hot-reloads, is easiest for 20 contributors to pick up, and exports to headless Linux with no runtime. C# only pays off if the team already lives in C#. |
| D2 | **3D or 2D?** | **3D** (third-person, fixed-ish top-down/iso camera) | The project is already set up 3D + Jolt; GDD refs (Gang Beasts, For Honor) are 3D; ragdolls are required content. A high camera keeps 20-player readability. |
| D3 | **Networking stack** | Godot high-level multiplayer over **ENet**, dedicated headless server on a VPS | Resolves the GDD §12 Steam question: free, self-hosted, fits "existing infra experience". |
| D4 | **Jump / hop** | Build no-jump first; A/B a low hop in Phase 2 | GDD §5.1 leaves it `TBD`. |
| D5 | Git | `git init` in Phase 0 (folder is not a repo yet) | Tuning file must be version-controlled per GDD §9. |

## Change log
- 2026-09-14 — v0.1 plan created. Awaiting confirmation on D1–D5.
