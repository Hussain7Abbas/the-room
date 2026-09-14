# THE ROOM — Character & Ability Spec

Copy this file to `characters/<your-character-id>/SPEC.md` and fill it in. Read the full
grammar in `/CHARACTER-SPEC.md` at the repo root first — this is just the fillable template
(Part 2), reproduced here so it lives next to the character it describes. The review gate
(Part 3 of the root doc) is what actually has to pass before any art or animation work starts.

---

**Character name:**
**Designed and owned by:**
**One-line personality:** *(the joke — what about you is this character?)*

### Ability
**Name:**
**Archetype slot:**
**What the player presses:**
**What happens, in order, with timings:**

```
0.00s  input
0.0Xs  tell fires (audio: ___, visual: ___)
0.Xs   effect begins
X.Xs   effect ends
```

**Cooldown:**
**Power budget:** strong = ___ , weak = ___
**Counterplay (one sentence):**
**What dynamic does this create in actual play?**

### Passive quirk
**Passive:**
**Why it fits the character's personality:**

### Identity
**Silhouette:**
**Signature colour:**
**Voice line on kill:**
**Voice line on death:**
**Voice line on dodge:**
**Death animation notes:**

### Failure cases
**When is this ability useless?**
**What's the most annoying way someone could abuse this?**

---

## Implementation checklist (once the spec above passes review)
- [ ] `AbilityDef` added to `abilities/CharacterRegistry.cs` (or its own resource, once the
      registry grows past hand-authored C# instances).
- [ ] Ability class in `abilities/<YourAbility>.cs`, extending `abilities/Ability.cs`.
- [ ] Numbers added to `tuning/tuning.tres` under `AbilityNumbers`, keyed `<id>.<param>`
      (never hardcoded in the ability class itself).
- [ ] `AbilityValidator.RunAndPrint()` (runs automatically at startup) passes with no errors
      for your character.
- [ ] Played in a real match against at least one other ability.
