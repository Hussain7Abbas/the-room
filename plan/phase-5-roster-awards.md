# Phase 5 — Six Characters, Art Pass, Awards (GDD M4)

**Goal:** minimum viable roster of 6, readable at a glance, and a results screen that is the payoff of the session.

**Gate question:** *Does the roster read at a glance in a crowd?* Can a spectator identify a character from silhouette and ability audio alone?

## Tasks
### Roster
- [ ] **4 more characters — deliberately not built here.** Pillar 3 explicitly rejects "anyone building a character for someone else" and "characters designed by committee." Claude authoring 4 more placeholder characters (on top of Phase 3's two framework examples, which were already flagged as needing replacement, not extension) would be exactly that. This is genuinely blocked on real developers claiming archetype slots and writing their own `SPEC.md` — see `characters/_template/SPEC.md`, ready for them.
- [ ] **Art pass — deliberately not built here**, same reasoning, plus the standing rule from `plan/main.md` D3: no art assets without a real character to attach them to and no auto-downloaded/generated assets without the user explicitly picking them.
- [x] **VFX density budget tested at 20 players, not 4.** Ran a real 20-bot stress test (the full `Net.MaxPlayers` cap) for 90s: all 20 connected, a full match cycle completed (including Last Call and all four award types firing at least once across the session), 29 combat events, 42 match events, **zero errors** across the server log and all 20 individual bot logs. This validates *server stability and correctness* at target scale — it does not and cannot validate visual VFX density, since there's no real VFX yet (grey-box color flashes only) to test for burying tells.

### Awards / results screen
- [ ] Short results screen UI + rematch vote — **not built**. `MatchServer`'s match loop (Phase 4) already auto-resets on a timer, which covers the *mechanical* "does a match end and start again" question; a real UI screen showing final standings before the reset is genuinely a UI task, deferred alongside the rest of Phase 2/4's UI gaps.
- [x] **Award titles computed from match telemetry.** `core/MatchServer.cs`: tracks parries, heavy whiffs, executes-taken (deaths to an execute), and biggest single bounty collected, server-side, per match, reset every rematch. At match end, computes and announces one line per award that actually happened this match (a stat of zero doesn't get a title). Implemented: **"Most Stabbed In The Back"** (most deaths to an execute), **"Sharpest Reflexes"** (most parries), **"All Bark, No Bite"** (most whiffed heavies), **"Highway Robbery"** (biggest single bounty collected) — all four names from the GDD's own examples or in that spirit.
  - **Verified working, not just written**: a shortened test match printed `ALL BARK, NO BITE: Bot1 (8 whiffed heavies)` and `HIGHWAY ROBBERY: Bot1 (1 pts in one bounty)`; the 20-player stress test separately triggered `MOST STABBED IN THE BACK: Bot5 (1 times)`. Confirmed the system correctly *omits* an award category when nobody triggered it that match, rather than always printing all four.
- [ ] Victory pose / funny presentation — **not built**, needs real character animations that don't exist yet (Pillar 1 requirement, but blocked on the same roster/art gap above).

### Readability audit
- [ ] Silhouette test, audio test — **cannot be done meaningfully** with two placeholder characters that are colour-only and have no audio. Genuinely blocked on real characters existing first.

## Out of scope
Persistent data, webhooks, cosmetics (Phase 6).

## Where this phase actually stands
The **systems** half of Phase 5 that doesn't require real characters is done and verified at scale (award computation, 20-player stability). Everything else in this phase — the roster, the art pass, the readability audit, victory poses — is genuinely blocked on real developers doing the thing Pillar 3 says only they should do. Continuing to generate placeholder content here would work against the pillar this phase exists to serve, not toward it.
