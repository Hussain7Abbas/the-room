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
- [ ] Bounty system + announcer/callout UI.
- [ ] Golden Knife pickup, outline shader, scoring multiplier.
- [ ] Last Call phase controller + shutters + lighting.
- [ ] Map sections gated by player count; hazard.
- [ ] Telemetry: match length, lead changes, bounty claims, knife pickups by player rank.

## Playtest questions
- Median match length < 10 min?
- Are bounties getting claimed? Is the knife pulling players to the centre?
- Does anyone turtle? (If yes, Last Call needs to close harder.)
