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
- [x] **Combat state machine + server resolution of all verbs** (`player/Player.cs`): `Idle → {LightWindup, HeavyWindup, Parrying} → Recovery/Staggered/Executing → Idle`, plus terminal `Dead`. Light, heavy lunge (`Player.ServerSweep` closes the gap, stopping at walls/pillars/players — originally `MoveAndCollide`, which, measured later, moved 0m on 23 of 26 lunges because a capsule resting on the floor reports the floor as its first collision; hits still registered only because the hit check reaches the full range from wherever the attacker stood), parry (negates one hit, staggers the attacker), dash (pure movement override, no i-frames, gated to `Idle` so it can't undermine heavy's whiff-punish window), execute (heavy from directly behind the victim's own facing = instant kill). Hit resolution reuses Phase 1's rewind lag compensation (`core/CombatServer.cs`, generalized to take range/radius per verb).
  - **Verified by running it**, not just reading it: 6 headless bots, 90s, 18 kills across light/heavy/execute, zero errors; a separate targeted run with boosted parry odds confirmed parry-negates-hit against both light and heavy. See "Testing" below for two real bugs this surfaced and fixed.
- [x] Health, death, **procedural primitive ragdoll** on kill: a plain `RigidBody3D` capsule dropped at the death position with a small random impulse, cosmetic only, no networking (each client spawns its own), self-frees after 3s. This is the grey-box stand-in the plan called for — no external art asset. 1.5s respawn (`Tuning.RespawnTime`), 1.5s spawn protection (`Tuning.SpawnProtectionDuration`) with a visual shimmer (emissive flash on the grey-box capsule material — real shader work is Phase 5), cancelled instantly on any verb use including dash.
  - **Not done: "spawn furthest from largest cluster."** Respawn still picks a uniformly random spawn marker (`Main.PickRandomSpawn`) — the cluster-avoidance logic needs a real occupancy check across spawn points, deferred rather than half-built.
- [ ] **Feedback: hit spark, directional hit sound, hitstop, confetti/blood burst, screen shake — deferred, needs assets.** All presentation-only per Pillar 1, but genuinely need sound/VFX assets nobody has picked yet. The one grey-box stand-in built now is the spawn-protection shimmer (color flash, no asset needed). This is the biggest real gap against the gate question — a duel with zero audio tells will read very differently than the final feel; flagging it plainly rather than pretending color-only feedback is equivalent.
- [x] **Killfeed** naming killer + method (`core/KillfeedUI.cs`/`.tscn`): listens to a new `Events.PlayerKilled` signal, shows "X killed Y (light/heavy/execute)" for 6s, max 5 lines. **1.5s death cam**: on your own death, camera turns to face the killer's last position for `Tuning.DeathCamDuration`, then respawn takes over.
  - Server-side confirmed via `[Combat] X killed Y (method)` log lines across every test run. The UI itself (killfeed text, death cam framing) is code-reviewed but **not visually verified** — this environment has no GUI to look at it with; needs an actual look by whoever plays it first.
- [ ] Off-screen attacker directional indicator — not built. Same category as the audio/VFX tells: valuable for the "who hit me" gate question, but a distinct enough chunk of UI work that it shouldn't get a rushed version bolted onto this pass.
- [x] Scoreboard (Tab) — `core/KillfeedUI.cs`, held to show K/D per connected player, sorted by kills. Same "not visually verified" caveat as the killfeed.
- [ ] Score target + timer, simple results screen, rematch — **not built.** This turned out to overlap materially with Phase 4's own "match shape" scope (Bounty/Golden Knife/Last Call already own "does a match have an ending"); building a real results-screen-and-rematch flow now would mean building it twice. Left for Phase 4, noted there.
- [ ] Deathmatch configs (Duel Pit 6–10 / Chaos 12–20) — not built; there's one static room and no lobby/config-selection UI yet. Low cost to add once Phase 4's match flow exists to select into.
- [x] **Hop A/B test scaffolding**: `Tuning.HopEnabled` + `Tuning.HopImpulse` in `tuning.tres` — flip the bool, no rebuild, no code change, to A/B in a real playtest. Actual jump/hop behavior (`Player.SimulateStep`) is implemented and reachable via the new `jump` input action (V key). The **decision itself is explicitly not made here** — that's a playtest call per the GDD, not something to default silently.
- [ ] Telemetry log per match (structured, queryable) — not built. What exists is `GD.Print` lines for every kill/parry, which is enough to have verified this phase's own testing but isn't the queryable per-match log the task describes (cause of every death, on-screen-or-not, parry attempt/success rates). Worth building once real human playtests are happening and there's data worth logging in a structured form — right now it'd be logging bot noise.

## Testing
Ran headless server + 6 headless bots (`--bot`) repeatedly, 30–90s each, watching for errors and reading `[Combat]` log lines rather than trusting the code by inspection. Two real bugs found and fixed this way:
1. **A logic bug**: a heavy hit unconditionally set the victim to `Staggered` afterward — including when that same hit had *just killed them*, silently resurrecting `Dead` back to `Staggered`. Fixed by skipping the stagger assignment when the victim is already dead.
2. **A Godot lifecycle bug**, root-caused via full C# stack trace rather than guessed at: the death-effect ragdoll spawner set a freshly-constructed `RigidBody3D`'s `GlobalPosition` via object initializer *before* adding it to the scene tree. `GlobalPosition`'s setter has to read the current global transform to compute a parent-relative offset, and on a still-parentless node that read fails (logs an engine error, silently falls back to identity — didn't crash, but was noise on every single kill). Fixed by setting local `Position` instead, which is equivalent before the node has a parent and has no tree dependency.

Final verification run (production tuning restored, not the tightened test-only values used mid-session): 6 bots, 30s, 3 kills (light/execute mix), **zero errors**.

## Playtest questions
- Can players say in one sentence what killed them, every time? — Mechanically yes (killfeed text is unambiguous); can't fully answer without audio/VFX tells, which are still missing.
- Does anyone parry on purpose by session three? — Needs human playtesters; bots parry at a fixed random rate, not a meaningful signal here.
- % deaths from an unseen attacker? — Not measured; needs the telemetry log this phase deferred.
- Hop or no hop? — Scaffolding is ready (`Tuning.HopEnabled`); the actual A/B needs a human session.

## Out of scope
Abilities, bounty, golden knife, art. (Confirmed: nothing here touches those.)
