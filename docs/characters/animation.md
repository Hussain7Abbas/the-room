# Animation pipeline

**Goal:** every character plays the same animations, and a new model or a new clip needs no code.

## How it works

Godot's **humanoid retarget** runs at import time on both models and animation clips:

1. A `BoneMap` resource with the `SkeletonProfileHumanoid` profile maps a rig's own bone names
   (`mixamorig_LeftArm`) to standard names (`LeftUpperArm`).
2. The importer renames the bones and names the skeleton **`GeneralSkeleton`**.
3. It normalises hip height, so position keys scale to each body's proportions.
4. Animation tracks end up addressed as `%GeneralSkeleton:<StandardBone>`. They don't depend on
   which rig they came from.

Any retargeted model can therefore play any retargeted clip.
`tests/CharacterAnimationTests.cs` enforces that every clip only targets bones every registered
model has.

| File | Role |
|---|---|
| `assets/animations/humanoid/mixamo_bone_map.tres` | Mixamo → humanoid map, works for any Mixamo rig |
| `assets/animations/humanoid/{run,jump,stab}.fbx` | clips, imported as **AnimationLibrary** |
| `assets/animations/humanoid/humanoid_default.tres` | `HumanoidAnimationSet`: which clip plays for which move, attack slices and durations |
| `animation/CharacterModel.cs` | builds the playable clips and picks one each frame |

## What CharacterModel does with the clips

- **Strips root motion.** Mixamo clips often walk the hips forward, but the server-authoritative
  body moves the character, so the hips' X/Z are pinned to the first key. The jump also has its
  upward lift clamped, since physics already raises the body.
- **Matches run speed to movement.** The run plays faster or slower with the player's speed,
  measured against how fast the clip was authored, so feet don't slide.
- **Slices and time-scales attacks.** Each attack plays the slice `*ClipStart`–`*ClipEnd` of its
  clip, stretched to `*AttackDuration`, so the strike lands as the windup finishes.
- **Idle** uses the `Idle` clip if the set has one; otherwise it holds the light-attack clip's
  first frame, a fighting stance.
- **Attacks play only when the server says so** (`Player.BroadcastAttackCue`).

## Adding or replacing a clip

1. Download from Mixamo: **FBX, Without Skin, 30 fps**. "In Place" is optional, since root motion is
   stripped anyway.
2. Save it as `assets/animations/humanoid/<name>.fbx`, then set up its `.import` like `run.fbx.import`:
   - `importer="animation_library"` and `type="AnimationLibrary"`;
   - the same bone-map `_subresources` block.
3. Point a field of `humanoid_default.tres` at it. To give one character different clips, create
   another `HumanoidAnimationSet` and set it as that `CharacterDef.Animations`.
4. **For attacks,** choose the slice by eye:
   - temporarily set the attack's duration to the clip's full length;
   - run `make preview-animations` and note when the strike happens;
   - set `ClipStart`/`ClipEnd` around it;
   - set `AttackDuration` to windup + recovery from `tuning.tres`.
5. Run `make test`.

**A new kind of move** (say, a parry animation) needs a little code:

- a `Clip` enum value and a `HumanoidAnimationSet` field;
- handling in `CharacterModel.BuildLibrary`;
- a trigger from `Player`. For combat moves, cue it from a server broadcast.

## Non-Mixamo rigs

For Rigify, VRoid or other rigs, create a new `BoneMap` in the Advanced Import dialog:

- set the profile to `SkeletonProfileHumanoid`;
- Godot auto-fills most bones;
- use that map instead of the Mixamo one when importing.

The clips don't change: they only know the standard names.

## Current clips

| Move | Clip | Slice | Plays in |
|---|---|---|---|
| Idle | first frame of `stab.fbx` | — | loops |
| Run | `run.fbx` (Mixamo "Fast Run") | whole | loops, speed-matched |
| Jump | `jump.fbx` | whole | while airborne |
| Light attack | `stab.fbx` | 0.7–1.3 s | 0.35 s |
| Heavy attack | `stab.fbx` | 0.2–2.1 s | 1.0 s |

Missing today: dedicated idle, parry, dash, hit-react and death clips. The death effect is still a
grey-box capsule.
