# Phase 3 — Ability Framework + First Two Abilities (GDD M2)

**Goal:** a framework that makes the `CHARACTER-SPEC.md` grammar enforceable in code, then two abilities by two different owners built on it.

**Gate question:** *Does the ability grammar survive contact with two people designing independently?*

## Framework
- `Ability` base class (server-authoritative) with enforced lifecycle:
  `input → tell (audio + VFX, ≥0.3s) → effect → end → cooldown`.
  The base class **refuses** a tell shorter than `Tuning.min_tell_time` (0.3s) and control durations > 1.0s — the hard rules live in code, not in reviews.
- `AbilityDef` resource: archetype slot (enum of 8), power budget (strong / weak from `damage · movement · control · information`), cooldown 12–25s or charges, tell sound, tell VFX scene.
  - Numbers in `AbilityDef` are read from the central tuning file keyed by ability id — owners edit shape, not strength (CHARACTER-SPEC Part 4).
- `CharacterDef` resource: name, owner, colour, silhouette scene, passive quirk, voice lines (kill / death / parry), death animation.
- Shared-kit helpers abilities may use: apply_displacement, apply_slow (≤1s), spawn_zone, spawn_trap, reveal. No ability may add new resources/stats/UI (hard ban).
- Killfeed shows ability name as the kill method.
- Validator tool (editor script): scans all `AbilityDef`s and flags slot collisions, out-of-range cooldowns, missing tells, missing counterplay text.

## Tasks
- [ ] Ability base class, `AbilityDef`, `CharacterDef`, validator.
- [ ] Character select screen (grey-box: coloured capsules + name).
- [ ] Spec template copied to `characters/_template/SPEC.md`; each character folder holds its filled spec.
- [ ] Two owners fill specs → **7-question review gate** → build grey-box (primitive shapes, placeholder sounds).
- [ ] Playtest with both abilities in a real match.

## Playtest questions
- Is each tell audible in a crowd with other things going off?
- Can victims name the ability that killed them?
- Did either owner need a new shared system? (If so → separate proposal.)
