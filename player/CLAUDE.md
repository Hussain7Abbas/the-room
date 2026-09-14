# player/ — the Player controller

> **Keep this file in sync** with `Player.cs` / `Player.tscn`. When a role, RPC, input path or
> combat rule changes, update this file and `docs/architecture/networking.md` /
> `docs/gameplay/combat.md` in the same change.

`Player.cs` is large (~1200 lines) and split into sections. Keep new code in the matching one.

## One class, several roles (decided in `_Ready`)

| Role | Who | What runs |
|---|---|---|
| Server | the dedicated server's copy | authoritative `SimulateStep`, combat state machine, abilities, broadcasts |
| Owner | the local human's copy | client prediction + `SubmitInput` every physics tick, reconciliation |
| Remote view | everyone else's copy | `InterpolateRemote()` only, no physics |
| Offline | practice mode | owner + server in one |
| Bot | an owner with `--bot` | `RunBotAi` fills the input instead of the keyboard |

## Rules

- **Movement goes through `SimulateStep`**, the step shared by server, owner and offline.
  Displacing moves (heavy lunge, Drop Kick) use `ServerSweep`, never a raw
  `GlobalPosition =`. Teleports end up inside geometry.
- **Camera and body turn separately.** The mouse changes `_cameraYaw` (the pivot is pinned to it);
  `UpdateFacing` turns the body toward the move direction; `FaceAim()` + the aim lock face the
  camera for attacks and abilities (a dodge faces its roll direction instead). Never `RotateY` the body from mouse input.
- **Input to the server:** `SubmitInput(tick, worldDir, bodyYaw, jumpCounter)` is unreliable.
  `worldDir` is world-space (camera-relative for humans). Verbs and abilities carry the yaw too. One-shot
  actions travel as counters, so a dropped packet doesn't eat them (see the jump). Verbs use the
  reliable `RequestVerb`.
- **Dodge** is the one verb the owner predicts: `RequestVerbLocal` starts the roll locally
  (`StartDodge(predicted: true)`) and the server runs it for real. `IsDodging` hides the hitbox
  in `CombatServer`. There is no parry and no dash any more.
- **Sprint** is Shift held, sent every tick in `SubmitInput` and applied in `SimulateStep`.
- **Stamina** (`UpdateStamina`, `SpendStamina`) gates sprint and dodge. It runs in `SimulateStep`,
  on the server (authoritative) and on the owner (predicted). `ReceiveServerState` carries health
  and stamina; the owner snaps its stamina only when it drifts by more than 12.
- An ability whose character has an `Ability` clip acts it out instead of flashing
  (`ReceiveAbilityTell`). The owner's cooldown for the HUD also starts there.
- Combat state (`_combatState`) is **server-only**. Clients learn about it only through
  broadcasts (`BroadcastKill`, `BroadcastAttackCue`, `ReceiveAbilityTell`, …).
- Visuals:
  - `_model` (`animation/CharacterModel`) replaces the capsule when not headless;
  - tint and flash through `_bodyMaterials`;
  - attack animations only ever play from the server's `BroadcastAttackCue`;
  - the stab sound plays only from `BroadcastHitSound`, sent when the server confirms a landed
    hit in `ResolveMeleeAttack`. It's positional `AudioStreamPlayer3D`, and headless peers skip it.
- **Timers that drive visuals count down on every copy** (top of `_PhysicsProcess`), and the server
  sets and cancels them with a broadcast (`SetSpawnProtection`). A timer that only ticks on the
  server leaves clients stuck: the "white character" bug.
- Local input is ignored while `GameMenu.IsOpen`. Esc belongs to `ui/GameMenu`, not Player.
- Identity (name + character) syncs with `AnnounceIdentity` / `ReceiveIdentity`, plus
  `RequestIdentity` for late joiners. MultiplayerSpawner doesn't replicate properties changed
  after spawn.
- `Player.tscn`: capsule 1.8 m. The SpringArm must stay **unrotated**, or the camera ends up in
  front of the player and controls reverse (`tests/CameraRigTests.cs` guards this).
