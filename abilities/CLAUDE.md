# abilities/ — ability grammar framework

> **Keep this file in sync** with the code here. When the lifecycle, validator rules, registry
> or shared kit change, update this file and `docs/gameplay/abilities.md` in the same change.

## Pieces

- `Ability.cs`: base class. Enforces **input → tell (≥ `Tuning.MinAbilityTellTime`) → effect →
  cooldown (12–25 s)** in code. `TryActivate()` refuses an ability that breaks the grammar.
- `AbilityDef.cs` / `CharacterDef.cs`: Godot Resources holding identity and *shape*. Strength
  numbers live in `tuning.tres` → `AbilityNumbers` (`"<abilityId>.<param>"`).
- `CharacterDef` also carries `Model`, `Animations` and `TintModel` (see `animation/`).
- `CharacterRegistry.cs`: every playable character, built in C#. **`zain`** (Drop Kick) is the
  team's own character and the default pick. **`blink` and `firepatch` are placeholder examples
  written by Claude, not real submissions.** Replace them, don't build on them.
  - Load character assets **by path** (`AnimationsPath`), never with `GD.Load` in the registry.
    It runs on the headless server, which has no imported FBX files.
- `AbilityValidator.cs`: runs on every boot and in tests. Flags slot collisions, bad cooldowns,
  missing tell or counterplay text.
- The shared kit (via the `Ability` helpers → `Player.Server*`): `ServerSweep` movement,
  `Strike` (a melee hit resolved like the knife), slow (≤ 1 s), reveal, `SpawnDamageZone` /
  `AbilityZone`.

## Rules

- A new ability needs a real owner and a filled `characters/<id>/SPEC.md` (Pillar 3).
- The code controls shape; `tuning.tres` controls numbers. Read them with
  `Tuning.GetAbilityNumber(id, param, fallback)`.
- Only the server runs ability logic. Visible tells go out as broadcasts
  (`Player.BroadcastAbilityTell`).
- Don't add new resources, stats or UI for a single ability. If one is needed, raise it as a
  shared-kit proposal.
- Register new ability classes in `Player.CreateAbility`.
