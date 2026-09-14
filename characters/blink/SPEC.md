# THE ROOM — Character & Ability Spec

> **This is a Phase 3 framework example, not a real character submission.** Claude wrote it to
> exercise the ability grammar end-to-end (see `plan/phase-3-abilities.md`) and prove the
> `Ability` base class actually enforces the hard rules in code. Per Pillar 3 — "every character
> is a recognisable caricature of the person who made them" — a real character needs a real
> developer writing their own spec and owning their own concept. Replace this file (and
> `abilities/CharacterRegistry.cs`'s `"blink"` entry) the moment an actual person claims the
> Mobility slot; don't build on top of it as if it were real.

---

**Character name:** Blink Test Character
**Designed and owned by:** *(placeholder — nobody yet)*
**One-line personality:** Never actually there when you swing.

### Ability
**Name:** Blink
**Archetype slot:** Mobility
**What the player presses:** `ability` (E)
**What happens, in order, with timings:**

```
0.00s  input
0.00s  tell fires (visual: whole body flashes bright blue; audio: none yet — no asset)
0.30s  effect: teleports 6m in the direction the caster is facing
0.30s  effect ends (instant — no duration to speak of)
```

**Cooldown:** 14s
**Power budget:** strong = movement, weak = none
**Counterplay (one sentence):** You can reposition, attack, or parry during the 0.3s flash before the blink actually happens — it's not instant, and it doesn't grant any damage, healing, or invulnerability.
**What dynamic does this create in actual play?** Forces the victim to decide, the instant they see the flash, whether to commit to a punish on a now-repositioning target or hold their spacing — guessing wrong either way costs them something.

### Passive quirk
**Passive:** 5% faster acceleration out of a standstill.
**Why it fits the character's personality:** Small, flavour-only, doesn't change a duel's outcome — matches the ability's whole identity of "always just out of reach."

### Identity
**Silhouette:** Grey-box capsule only (Phase 5 art pass). Signature colour is the only distinguishing feature right now.
**Signature colour:** Blue (`#4088FF`)
**Voice line on kill:** *(placeholder — no audio assets yet)*
**Voice line on death:** *(placeholder)*
**Voice line on parry:** *(placeholder)*
**Death animation notes:** *(placeholder — Phase 5)*

### Failure cases
**When is this ability useless?** As a panic button mid-exchange — the 0.3s tell means anyone already swinging at you will land their hit before you're gone. It only helps you get TO or AWAY FROM a fight that hasn't started yet, never one already in progress.
**What's the most annoying way someone could abuse this?** Blinking through a pillar to break line of sight mid-duel, denying an opponent's read on where you'll re-engage from — annoying, but not a grammar violation (it doesn't remove their control, doesn't deal unavoidable damage, and the flash still told them it was coming).

---

## Review gate (CHARACTER-SPEC.md Part 3) — self-check, not a substitute for a real team review

1. **Fits one archetype slot, stays in budget?** Yes — Mobility, strong=movement only, no weak axis touched.
2. **Counterplay describable without the owner's help?** Yes, see above; it's one sentence and doesn't require knowing anything about the implementation.
3. **Would a hit victim say what happened in one sentence?** N/A — Blink deals no damage, so there's no "what killed them" to answer. (If a future Mobility ability *does* deal damage, this question becomes load-bearing.)
4. **Tell audible in a 20-player fight?** Visual only right now (no audio asset) — genuinely uncertain this would read at 20 players without a sound cue. Flagging honestly rather than claiming it passes.
5. **Creates a decision for the victim, not just an outcome?** Yes, per the "dynamic created" answer above.
6. **Recognisable from silhouette/audio alone?** Colour only, no silhouette shape difference yet (Phase 5). Marginal pass at best.
7. **Needs a new shared system?** No — `ServerTeleport` is a one-line addition to Player.cs, not a new system.

**Verdict: mechanically sound, honestly incomplete on 4 and 6 for lack of real assets.** Good enough to validate the framework (this phase's actual goal); not good enough to ship as a real character without Phase 5 art/audio.
