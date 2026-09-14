# animation/ — character models and shared humanoid animations

> **Keep this file in sync** with `CharacterModel.cs`, `HumanoidAnimationSet.cs` and the import
> setup in `assets/`. When the retarget contract, clip list or model rules change, update this
> file and `docs/characters/` in the same change.

## The contract that makes animations pluggable

- Every model **and** every animation FBX is imported with Godot's humanoid retarget, using a
  `BoneMap` with `SkeletonProfileHumanoid`. For Mixamo rigs that's
  `assets/animations/humanoid/mixamo_bone_map.tres`.
- The result is a skeleton named `GeneralSkeleton` with standard bone names, and clip tracks
  addressed `%GeneralSkeleton:<Bone>`. Any retargeted model plays any retargeted clip.
- Animation FBXs are imported as **AnimationLibrary** (`importer="animation_library"`).
- `tests/CharacterAnimationTests.cs` fails if any registered model lacks a bone a clip needs.

## CharacterModel

- It is cosmetic only; collision stays on the Player capsule. It is skipped when headless.
- It scales the model to the capsule height, using the highest bone in the rest pose.
- It rotates the model 180°, because humanoids face +Z and Player faces −Z.
- It strips root motion: the hips keep their first key's X/Z. The jump also clamps upward lift,
  because physics already moves the body.
- It picks clips from velocity and floor contact. Attack one-shots come only from `PlayAttack`,
  which the server cues.
- Attack clips are **sliced** (`*ClipStart`/`*ClipEnd`) and time-scaled to `*AttackDuration`, so
  the strike lands at the end of the windup. Pick slices by rendering, with
  `make preview-animations`.

## Rules

- Never animate position from a clip; the server-authoritative body moves the character.
- New clip types need a `Clip` enum entry, a `HumanoidAnimationSet` field, `BuildLibrary`
  handling and a trigger in `Player.UpdateModelAnimation`.
- After any change, check it in `make preview-animations` and in a rendered match.
