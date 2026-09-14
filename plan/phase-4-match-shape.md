# Phase 4 — Match Shape (GDD M3)

**Goal:** a match has a beginning, a climax, and a hard ending in under ten minutes (Pillar 4).

**Gate question:** *Does a match have a shape and an ending?* Plus: does the bottom-quartile player rematch voluntarily?

## Systems
### Bounty (GDD §5.5)
- +1 per kill without dying; announcements at 3 ("ON A TEAR"), 5, 8.
- Killing a bountied player awards their bounty in points + loud global callout naming both.
- Bountied players visibly marked (tunable threshold).

### Golden Knife (GDD §5.4)
- Spawns centre at 45s; respawns 30s after each use expires.
- Holder: one-hit kills for 20s, outlined for everyone (through walls), worth double points.
- Tell on pickup: global audio sting.

### Last Call (GDD §5.6)
- Triggers at 75% of score target: shutters close toward centre (animated wall geometry + kill/push volume with warning), lighting shift, bounties double, respawn 1s.
- Must also trigger on timer (e.g. last 90s) so timer-ended matches also climax — confirm in playtest.

### Map modularity (GDD §10)
- Outer shutter-gated sections open by lobby size (Duel Pit vs Chaos).
- One environmental hazard (tell-first, never unavoidable).

## Tasks
- [x] **Score target + timer, results, rematch** (carried over from Phase 2, built here alongside Last Call as planned). `core/MatchServer.cs`: continuous match loop — `InProgress → LastCall → Ended(results) → InProgress` (auto-reset, not a manual "rematch vote" UI — see below). Score target read from `Tuning.ScoreTargetDuelPit`/`ScoreTargetChaos`; timer from `Tuning.MatchTimeLimitSeconds`. On end: MVP determined by highest score, announced, `Tuning.ResultsScreenDurationSeconds` later every player is reset (`Player.ServerMatchReset`) and a new match starts automatically.
  - **Simplification, noted honestly**: "rematch vote" from the task list is actually just an automatic restart — there's no lobby to vote in yet, and with a persistent dedicated server that's arguably fine (GDD's "immediate rematch vote" matters more once there's a results *screen* people are looking at, which needs Phase 5's UI pass).
- [x] **Duel Pit / Chaos config selection** (carried over from Phase 2): `--config=duelpit|chaos` CLI flag (`core/Net.cs`), server-only, selects the score target. Same pattern as Phase 3's `--character` flag — no lobby UI yet, deferred for the same reason.
- [x] **Bounty system + announcer/callout.** `MatchServer.ServerRegisterKill`: +1 bounty per kill without dying, reset to 0 on death; killing a bountied player awards their bounty as bonus score plus a global callout ("X COLLECTED Y'S BOUNTY (+N)"); announcements at `Tuning.BountyAnnounceOnATear`(3)/`Second`(5)/`Third`(8). UI: a top-center announcement banner in `core/KillfeedUI.cs`/`.tscn`, fed by a new `Events.MatchAnnouncement` signal.
- [x] **Golden Knife pickup, scoring multiplier.** Spawns at room center (`Tuning.GoldenKnifeFirstSpawn`), respawns `GoldenKnifeRespawnDelay` after each use ends. Holder: one-hit kills on light *or* heavy for `GoldenKnifeDuration` (checked in `Player.ResolveMeleeAttack` before normal damage calc), worth `GoldenKnifeScoreMultiplier`× score, lost immediately if the holder dies. Pickup is a plain distance check (`Tuning.GoldenKnifePickupRadius`) against all registered players, server-only.
  - **Simplified, noted honestly**: no outline shader (visible-through-walls) — grey-box stand-in is a glowing gold box marker at the pickup point only, not a per-holder outline. Real outline needs Phase 5 shader work.
- [x] **Last Call phase controller.** Triggers at `LastCallThresholdPercent`(75%) of score target *or* inside `LastCallTimeRemainingSeconds`(90s) of the timer — both checked, so a timer-ended match still climaxes as the task called for. Effects: bounty-earning kills continue as normal (bounty doesn't literally double in this implementation — see below), respawn drops to `RespawnTimeLastCall`(1s).
  - **Not built, noted honestly**: real shutter geometry (animated walls closing toward center) and a lighting shift — this is level-design/art work on the one static room, not a systems gap, and doesn't belong bolted onto a match-logic pass. The grey-box stand-in is the announcement banner ("LAST CALL") plus the scoreboard header changing to say so. **Also not built**: literal bounty-doubling during Last Call specifically (GDD §5.6 says "bounties double") — the *scoring* (bounty-collection bonus) already exists, but a Last-Call-specific ×2 on top of it doesn't yet; cheap to add once the shutter/lighting work happens in the same pass.
- [ ] Map sections gated by player count; hazard. **Not built** — needs actual level geometry beyond the one static room; deferred to whenever map/level design work happens, not a systems task.
- [ ] Telemetry: match length, lead changes, bounty claims, knife pickups by player rank. **Not built as a structured log** — `[Match]`/`[Combat]` console prints exist and were exactly what verified this phase's own testing (see below), but there's still no queryable per-match file. Same call as Phase 2: not worth building until real human playtests are generating data worth logging.

## Testing
Ran headless server + bots with match timings temporarily shrunk (`ScoreTargetDuelPit=6`, `MatchTimeLimitSeconds=60`, `GoldenKnifeFirstSpawn=5s`, etc. — reverted to production values after) to actually observe a full match cycle rather than waiting 8 minutes:
- **Two complete match cycles** in one 90s/4-bot run: Golden Knife spawned, was picked up, expired/was lost, respawned again; bounty collected and announced; Last Call triggered; match ended with a correctly-computed MVP; **all players and match state auto-reset and a new match started cleanly** — twice.
- **Golden Knife one-hit-kill independently confirmed**: `[Combat] Bot2 killed Bot1 (golden knife)` — including via a *light* attack, which would never be lethal under normal damage rules, proving the one-hit override actually fires rather than just the pickup/visual working.
- Zero errors across every run, including a final sanity pass with **production timings restored** and `--config=chaos` (startup only — 480s matches don't complete in a short test window, but confirmed no crash/error with real numbers).

## Playtest questions
- Median match length < 10 min? Structurally yes (8-minute hard cap via `MatchTimeLimitSeconds`, Last Call forces a climax before it) — needs a real human match to confirm it *feels* like under ten minutes, not just measures as one.
- Are bounties getting claimed? Is the knife pulling players to the centre? Bots aren't a meaningful signal here (they don't rationally chase objectives) — the mechanics fire correctly, but "is it pulling players" is a human-playtest question.
- Does anyone turtle? (If yes, Last Call needs to close harder.) Can't answer without real shutter geometry to actually "close harder" — right now Last Call only speeds up respawn and puts a banner on screen, which may not be enough on its own. Flagging this as the most likely thing to need real work once map/level design happens.
