# Phase 2 — Grey-box Combat (GDD M1)

**Goal:** the three-verb duel (plus dash and execute) is fun with capsules and placeholder sounds. This is the skill ceiling of the whole game — tune it hardest.

**Gate question:** *Is the duel fun with zero art and zero abilities?* Plus: can every player say what killed them in one sentence?

## Mechanics (all numbers from `tuning.tres`)
| Verb | Behaviour |
|---|---|
| Light | ~0.12s wind-up, short range, ~35% HP |
| Heavy lunge | ~0.40s wind-up with visible + audible tell, closes ~3m, ~65% HP, staggers; long recovery on whiff |
| Parry | ~0.15s active window, negates one hit, staggers attacker 0.5s, ~3s CD; failed parry leaves you open |
| Execute | heavy from directly behind (angle threshold tunable) = instant kill, 0.6s lock |
| Dash | short burst, ~4s CD, **no i-frames** |

- Combat as a server-side state machine per player: `Idle → Windup → Active → Recovery`, plus `Stagger`, `Parrying`, `Executing`, `Dead`.
- RPS check: light beats parry-bait, heavy beats light range, parry beats heavy — write unit tests for each interaction.

## Tasks
- [ ] Combat state machine + server resolution of all verbs.
- [ ] Health, death, **ragdoll** on kill (Jolt), 1.5s respawn, spawn furthest from largest cluster, 1.5s spawn protection shimmer (cancelled on attack).
- [ ] Feedback: hit spark, directional hit sound, hitstop, confetti/blood burst, screen shake (all presentation-only — Pillar 1).
- [ ] **Killfeed** naming killer + method; **1.5s death cam** (Pillar 2 requirements).
- [ ] Off-screen attacker directional indicator.
- [ ] Scoreboard (Tab), score target + timer, simple results screen, rematch.
- [ ] Deathmatch configs: Duel Pit (6–10) and Chaos (12–20, score 40).
- [ ] Hop A/B test (D4): build behind a tuning flag, playtest both.
- [ ] Telemetry log per match: cause of every death, whether killer was on-screen for victim (target <40% unseen in Duel Pit), parry attempts/successes.

## Playtest questions
- Can players say in one sentence what killed them, every time?
- Does anyone parry on purpose by session three?
- % deaths from an unseen attacker?
- Hop or no hop?

## Out of scope
Abilities, bounty, golden knife, art.
