# THE ROOM — Character & Ability Spec

> Zain is the team's own character: the model every character currently wears, with the drop kick
> the Voidra team asked for on 2026-09-14. Numbers live in `tuning/tuning.tres` (`dropkick.*`).

**Character name:** Zain
**Designed and owned by:** Zain (Voidra team)
**One-line personality:** Settles every argument with both feet.

### Ability
**Name:** Drop Kick
**Archetype slot:** Burst
**What the player presses:** `ability` (E)
**What happens, in order, with timings:**

```
0.00s  input: Zain faces where the camera aims
0.00s  tell: crouch and leap (drop-kick animation) + cyan flash
0.45s  effect: lunges up to 3.5 m straight ahead (stops at walls/players), then both feet hit the
       first player within 1.8 m: 2× a light hit (70 damage) + stagger
~1.0s  lands and gets up (animation only)
```

**Cooldown:** 14s
**Power budget:** strong = damage, weak = movement
**Counterplay (one sentence):** Roll or sidestep during the 0.45s leap: it only hits what is straight in front of him when he lands.
**What dynamic does this create in actual play?** Standing still in front of Zain is a gamble, but a whiffed kick puts him on a 14s cooldown right in your face.

### Passive quirk
**Passive:** none yet.

### Identity
**Silhouette:** the Zain model (textured, knife in the right hand).
**Signature colour:** Green (`#59D973`)
**Voice lines:** placeholders, no audio assets yet.
**Death animation notes:** the shared Mixamo death clip.

### Failure cases
**When is this ability useless?** Against anyone who rolls through it or isn't dead ahead; it's a straight line, and the leap announces it.
**Most annoying abuse?** Kicking someone already staggered by a heavy. Both hits are telegraphed and dodgeable, so it stays readable.
