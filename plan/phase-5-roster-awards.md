# Phase 5 — Six Characters, Art Pass, Awards (GDD M4)

**Goal:** minimum viable roster of 6, readable at a glance, and a results screen that is the payoff of the session.

**Gate question:** *Does the roster read at a glance in a crowd?* Can a spectator identify a character from silhouette and ability audio alone?

## Tasks
### Roster
- [ ] 4 more characters through spec → review gate → grey-box → playtest (6 total, all 8 slots unique so far).
- [ ] Art pass **only** for characters that passed review and a real match: model, signature colour, silhouette, animations, voice lines (kill / death / parry), death animation, ragdoll.
- [ ] Owners build their own character (Pillar 3); centrally owned numbers stay in `tuning.tres`.
- [ ] VFX density budget tested at **20 players**, not 4. Cut VFX that bury tells.

### Awards / results screen
- [ ] Short results screen (nobody should leave during it) + rematch vote.
- [ ] Award titles computed from match telemetry ("Most Stabbed In The Back", most parries, biggest bounty claimed, most whiffed heavies, etc.).
- [ ] Victory pose / funny presentation (Pillar 1 — required content, not polish).

### Readability audit
- [ ] Silhouette test: screenshot 20-player fight in greyscale — can each character be named?
- [ ] Audio test: play each tell blind — can players name the owner?

## Out of scope
Persistent data, webhooks, cosmetics (Phase 6).
