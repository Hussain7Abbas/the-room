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
  Displacing moves (heavy lunge, Blink, future dashes) use `ServerSweep`, never a raw
  `GlobalPosition =`. Teleports end up inside geometry.
- **Input to the server:** `SubmitInput(tick, dir, yaw, jumpCounter)` is unreliable. One-shot
  actions travel as counters, so a dropped packet doesn't eat them (see the jump). Verbs use the
  reliable `RequestVerb`.
- Combat state (`_combatState`) is **server-only**. Clients learn about it only through
  broadcasts (`BroadcastKill`, `BroadcastAttackCue`, `ReceiveAbilityTell`, …).
- Visuals:
  - `_model` (`animation/CharacterModel`) replaces the capsule when not headless;
  - tint and flash through `_bodyMaterials`;
  - attack animations only ever play from the server's `BroadcastAttackCue`.
- Local input is ignored while `GameMenu.IsOpen`. Esc belongs to `ui/GameMenu`, not Player.
- Identity (name + character) syncs with `AnnounceIdentity` / `ReceiveIdentity`, plus
  `RequestIdentity` for late joiners. MultiplayerSpawner doesn't replicate properties changed
  after spawn.
- `Player.tscn`: capsule 1.8 m. The SpringArm must stay **unrotated**, or the camera ends up in
  front of the player and controls reverse (`tests/CameraRigTests.cs` guards this).
