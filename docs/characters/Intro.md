# Characters & animation

A character is three things:

| Part | Where | Owned by |
|---|---|---|
| **Identity and ability**: name, colour, ability, passive, spec answers | `abilities/CharacterRegistry.cs`, `characters/<id>/SPEC.md` | the developer the character caricatures (Pillar 3) |
| **Model**: any humanoid FBX | `assets/characters/<id>/` | the art owner |
| **Animations**: shared by everyone | `assets/animations/humanoid/` + `humanoid_default.tres` | the team |

Today the only model is **Zain** (`assets/characters/zain/zain.fbx`, Mixamo-rigged). Every character
without its own model uses Zain, tinted with the character's signature colour.

The shared animation set has **run, jump and stab**:

- the stab is used for both light and heavy attacks, with different slices;
- idle is the stab clip's opening stance.

## Pages

- [Adding a character](adding-a-character.md): bring in a new model and give it to a character.
- [Animation pipeline](animation.md): how retargeting works, adding clips, attack slices, and
  rigs that don't come from Mixamo.

## Code

- `animation/CharacterModel.cs` builds the visible body: scale, facing, materials, clip selection.
- `animation/HumanoidAnimationSet.cs` is the resource mapping moves to clips.
- `abilities/CharacterDef.cs` has the `Model`, `Animations` and `TintModel` fields.
- `tests/CharacterAnimationTests.cs` checks that every clip can play on every model.
- `tools/AnimationPreview.tscn`, run with `make preview-animations`.
