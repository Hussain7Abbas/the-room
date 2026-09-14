# Abilities

Every character has **one** ability. The grammar comes from the Character Spec and is **enforced
in code**, not just in reviews.

## The grammar

Every ability follows: **input → tell (≥ 0.3 s, visible) → effect → end → cooldown (12–25 s)**.

- `abilities/Ability.cs` refuses to activate an ability whose tell is too short or whose cooldown
  is out of range.
- Control effects (slow and similar) last 1 s at most (`MaxControlEffectDuration`).
- One archetype slot per ability (Mobility, Zone Denial, …) and a power budget: one strong axis
  and at most one weak axis (damage, movement, control, information).
- Counterplay must be describable in one sentence. The validator checks the text exists.
- `abilities/AbilityValidator.cs` runs on every boot and in `make test`.

## Current abilities

| Character | Ability | Slot | Effect | Cooldown |
|---|---|---|---|---|
| `zain` | **Drop Kick** | Burst (damage, weak: movement) | After a 0.45 s leap (the drop-kick animation is the tell; there's no colour flash), lunges up to 3.5 m and both feet hit whoever is straight ahead for **2× a light hit** (70), with a stagger. Dodgeable. | 14 s |
| `firepatch` | Fire Patch | Zone Denial | After a 0.3 s tell, throws an 8 m arc and leaves a 2.5 m zone dealing 20 damage/s for 4 s | 18 s |

Zain is the team's own character: the model every character uses, and the drop kick the team
designed, and the default pick. `firepatch` is a **placeholder example** written to prove the
framework. Per Pillar 3 a real character must be designed by the developer it caricatures, so
replace it rather than build on it. (The Blink example was removed.)

An ability can be acted out by the character: give the character its own `HumanoidAnimationSet`
with an `Ability` clip (see `assets/characters/zain/zain_animations.tres`). It plays on the
tell, sliced so the strike lands as the tell ends, and **replaces the colour flash**. Abilities
without an animation (Fire Patch) still flash.

## Adding an ability

1. The owning developer fills in `characters/<id>/SPEC.md` from `characters/_template/SPEC.md`.
2. Write `abilities/<Name>Ability.cs` deriving from `Ability`. Implement the effect; the tell and
   cooldown come from the base class.
3. Put **every number** in `tuning/tuning.tres` under `AbilityNumbers`, as `"<id>.<param>"`. At a
   minimum:
   - `tell_seconds`;
   - `cooldown`;
   - the ability's own numbers.
   Read them with `TuningService.Instance.GetAbilityNumber(Def.Id, "<param>", fallback)`.
4. Use the shared kit, never custom systems:
   - `Caster.ServerSweep` for movement;
   - `Strike(reach, radius, damage, staggers)` for a melee hit, resolved exactly like the knife
     (rewind, dodges, spawn protection, stab sound; used by Drop Kick);
   - `ApplySlow` (≤ 1 s);
   - `Reveal`;
   - `SpawnDamageZone`.
5. Add an `AbilityDef` + `CharacterDef` in `abilities/CharacterRegistry.cs`, and a case in
   `Player.CreateAbility`.
6. `make test` runs the validator. Then run bots with `--character=<id>` and check the server log.
7. Update this page.
