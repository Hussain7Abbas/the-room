# Adding a character model

This page covers the **model** only. A new playable character also needs an owner, a spec and an
ability; see [Abilities](../gameplay/abilities.md#adding-an-ability).

## 1. Get a humanoid FBX

The easiest route is [Mixamo](https://www.mixamo.com): upload or pick a character, let it
auto-rig, then download **FBX, T-pose, with skin**. Any humanoid rig works; see
[non-Mixamo rigs](animation.md#non-mixamo-rigs).

## 2. Put it in the project

```
assets/characters/<id>/<id>.fbx
```

## 3. Import it with the humanoid retarget

Choose one of these:

- **In the editor:** select the FBX → Import dock → **Advanced…** → select `Skeleton3D` → set
  **Retarget → Bone Map** to `res://assets/animations/humanoid/mixamo_bone_map.tres` → **Reimport**.
- **By hand:** open the generated `<id>.fbx.import` and replace `_subresources={}` with the block
  from `assets/characters/zain/zain.fbx.import`:

  ```
  _subresources={
  "nodes": {
  "PATH:Skeleton3D": {
  "retarget/bone_map": Resource("res://assets/animations/humanoid/mixamo_bone_map.tres")
  }
  }
  }
  ```

  Then run `godot --headless --path . --import`.

After import the skeleton is called `GeneralSkeleton` and uses standard bone names.

## 4. Look at it

```bash
make preview-animations MODEL=res://assets/characters/<id>/<id>.fbx
```

It shows a side view cycling idle → run → jump → light → heavy. Check that the model:

- stands on the floor;
- faces the way it runs;
- is roughly 1.8 m tall. Scaling is automatic, from the highest bone.

## 5. Give it to a character

In `abilities/CharacterRegistry.cs`, on the character's `CharacterDef`:

```csharp
Model = GD.Load<PackedScene>("res://assets/characters/<id>/<id>.fbx"),
TintModel = false, // set this if the model has its own textures (otherwise the colour tints them)
```

## 6. Test

```bash
make test
```

`CharacterAnimationTests` fails if the model is missing any bone the shared clips animate, which
usually means the import step was skipped. Then join a local room and look at it in a real match
(see [Testing](../testing.md#looking-at-the-game)).
