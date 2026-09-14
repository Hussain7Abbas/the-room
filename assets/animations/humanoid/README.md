# Humanoid animations: shared by every character

Every character plays the same clips: idle, run, jump, light attack and heavy attack.
This works because models and clips are imported with Godot's **humanoid retarget**.
The retarget renames each rig's bones to standard names (`Hips`, `Spine`, `LeftUpperArm` and so on) on a skeleton called `GeneralSkeleton`.
It also normalises the hip height, so the clips play on characters of different proportions.
`tests/CharacterAnimationTests.cs` fails if any model is missing a bone the clips need.

| File | What it is |
|---|---|
| `mixamo_bone_map.tres` | Maps Mixamo bone names to the standard humanoid names. It works for any Mixamo-rigged model or clip. |
| `humanoid_default.tres` | The `HumanoidAnimationSet`: which clip is used for each move, which slice of each attack clip plays, and how long it lasts. |
| `run.fbx`, `jump.fbx`, `stab.fbx` | Mixamo clips, imported as **Animation Library** with the bone map above. |
| `animation/CharacterModel.cs` | Scales any model to the 1.8m hit capsule and strips root motion. It then picks the clip from movement or from the server's attack cue. |

## Add a new character model (Mixamo)

1. Put the FBX at `assets/characters/<name>/<name>.fbx`.
2. In the editor, open the Import dock and click **Advanced…**. Select `Skeleton3D` and set **Retarget → Bone Map** to `res://assets/animations/humanoid/mixamo_bone_map.tres`, then click **Reimport**.
   You can also copy the `_subresources={...}` block from `assets/characters/zain/zain.fbx.import` into the new model's `.import` file.
3. Check it: `make preview-animations MODEL=res://assets/characters/<name>/<name>.fbx`
4. Set `Model = GD.Load<PackedScene>("res://assets/characters/<name>/<name>.fbx")` on the character's `CharacterDef` in `abilities/CharacterRegistry.cs`.
   A character without a `Model` uses Zain.
   If the model is textured, set `TintModel = false`; otherwise the character colour multiplies over the texture.
5. Run `make test`.

## Add or replace an animation

1. Download the clip from Mixamo. Pick "Without Skin" and 30 fps. "In Place" is optional because root motion is stripped anyway.
2. Drop it here and set up its import like `run.fbx.import`: `importer="animation_library"` plus the same bone-map `_subresources`.
3. Point a field of `humanoid_default.tres` at it, or give one character its own `HumanoidAnimationSet` through `CharacterDef.Animations`.
4. For an attack, set `*ClipStart`/`*ClipEnd` to the strike's slice, and `*AttackDuration` to windup plus recovery from `tuning/tuning.tres`.
   The slice is then sped up or slowed down so the strike lands when the server resolves the hit.
   Use `make preview-animations` to find the slice.

An `Idle` clip is optional. Without one, idle holds the first frame of the light-attack clip, which is a fighting stance in "Stabbing".

## Non-Mixamo rigs (Blender Rigify, VRoid, …)

Make a new `BoneMap` for that rig in the Advanced Import dialog, using the profile `SkeletonProfileHumanoid`. Godot auto-fills most names.
Then use that map in step 2 instead of the Mixamo one. The clips don't change, because they only use the standard names.
