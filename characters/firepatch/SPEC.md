# THE ROOM — Character & Ability Spec

> **This is a Phase 3 framework example, not a real character submission.** Claude wrote it to
> exercise the ability grammar end-to-end (see `plan/phase-3-abilities.md`) — specifically to
> prove the Zone Denial archetype and the `SpawnDamageZone` shared-kit helper actually work, in
> a real 6-bot playtest, not just in theory. Per Pillar 3, a real character needs a real
> developer writing their own spec. Replace this file (and `abilities/CharacterRegistry.cs`'s
> `"firepatch"` entry) the moment an actual person claims the Zone Denial slot.

---

**Character name:** Fire Patch Test Character
**Designed and owned by:** *(placeholder — nobody yet)*
**One-line personality:** Makes you pay a toll to walk through a doorway.

### Ability
**Name:** Fire Patch
**Archetype slot:** Zone Denial
**What the player presses:** `ability` (E)
**What happens, in order, with timings:**

```
0.00s  input
0.00s  tell fires (visual: orange flash on the caster; audio: none yet — no asset)
0.30s  effect: an 8m-forward, 2.5m-radius patch of ground starts ticking
0.30s–4.30s  anyone standing in the patch takes 20 damage/second
4.30s  patch despawns
```

**Cooldown:** 18s
**Power budget:** strong = damage, weak = none
**Counterplay (one sentence):** You can see exactly where the patch lands and how big it is before it starts ticking — just don't stand in it, or step out the moment you're in it.
**What dynamic does this create in actual play?** Turns a piece of ground into a toll: anyone who needs to cross it (a doorway, the Golden Knife plinth) has to decide whether the fight on the other side is worth the chip damage.

### Passive quirk
**Passive:** Footsteps 10% quieter.
**Why it fits the character's personality:** Small, flavour-only; a slightly sneakier fit for someone who wants you to walk into a trap you didn't hear coming.

### Identity
**Silhouette:** Grey-box capsule only (Phase 5 art pass).
**Signature colour:** Orange (`#FF5A1A`)
**Voice line on kill:** *(placeholder — no audio assets yet)*
**Voice line on death:** *(placeholder)*
**Voice line on dodge:** *(placeholder)*
**Death animation notes:** *(placeholder — Phase 5)*

### Failure cases
**When is this ability useless?** Against anyone already repositioning who was never going to cross that ground — the caster gets nothing back for throwing it on empty space, and the 18s cooldown is now spent for zero effect.
**What's the most annoying way someone could abuse this?** Zoning an entire doorway/chokepoint so a contested objective (Golden Knife) becomes a coin-flip of who's willing to eat chip damage first — this is the ability doing its job, not a bug, but it'll be the first thing a playtester complains about on a small map.

---

## Review gate (CHARACTER-SPEC.md Part 3) — self-check, not a substitute for a real team review

1. **Fits one archetype slot, stays in budget?** Yes — Zone Denial, strong=damage only, no weak axis touched. Damage is a DoT over standing-in-zone time, not a burst, which is what keeps it "zone denial" rather than drifting into Burst's territory.
2. **Counterplay describable without the owner's help?** Yes — "don't stand in the visible orange patch" needs no implementation knowledge.
3. **Would a hit victim say what killed them in one sentence?** Yes if it's the killing blow ("Fire Patch" shows in the killfeed as the method, same as light/heavy/execute) — verified directly: a real 90s bot test logged 137 individual damage ticks against one victim with correct per-tick amounts (0.67 = 20/30, matching the tick rate), and the mechanic was separately confirmed capable of a kill.
4. **Tell audible in a 20-player fight?** Visual only right now (no audio asset) — same honest gap as the other abilities. A DoT zone with no audio cue is a bigger readability risk than Blink's instant reposition, since the victim needs to notice they're taking damage from *this specific thing* among everything else going off.
5. **Creates a decision for the victim, not just an outcome?** Yes — see "dynamic created" above; the decision (cross or don't) exists the moment the patch lands, before any damage is taken.
6. **Recognisable from silhouette/audio alone?** Colour only (orange), no silhouette shape difference yet.
7. **Needs a new shared system?** No — built entirely on `AbilityZone` (the same Zone Denial/Trap shared-kit helper any future ability in these slots can reuse) and `Player.ServerApplyDamage` (already existed for melee).

**Verdict: mechanically sound and the actual damage path is verified working end-to-end, not just assumed** (see plan/phase-3-abilities.md's testing notes). Same honest gap as Blink on questions 4 and 6 — needs real audio/art before this reads well at higher player counts.
