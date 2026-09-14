# core/ — autoloads and the game scene

> **Keep this file in sync** with the code in `core/`. When you (or the user) add, rename or
> change an autoload, session rule or RPC pattern here, update this file and
> `docs/architecture/` in the same change.

## Autoloads (project.godot order matters: later ones may use earlier ones in `_Ready`)

| Autoload | File | Role |
|---|---|---|
| `InputDevices` | `InputDevices.cs` | Current device (keyboard / Xbox / PlayStation, switched by any press), applies saved bindings (`InputBindings.cs`), gamepad focus for menus |
| `Events` | `Events.cs` | Signal bus: PlayerConnected/Disconnected/Spawned/Killed, MatchAnnouncement |
| `Tuning` | `TuningService.cs` | Loads `tuning/tuning.tres` |
| `Net` | `Net.cs` | Session state machine (None / Offline / Server / Client), ENet, CLI flags, scene switching |
| `PingService` | `PingService.cs` | RTT echo RPCs; the server uses RTT/2 for hit rewind |
| `CombatServer` | `CombatServer.cs` | Hitbox history + rewind hit tests (server only) |
| `MatchServer` | `MatchServer.cs` | Match loop: score, bounty, Golden Knife, Last Call, results, awards, kills/deaths |
| `SeasonStats` | `SeasonStats.cs` | Legacy JSON season stats, **only for servers not started by the lobby** |
| `RoomReporter` | `RoomReporter.cs` | Lobby rooms only: heartbeat every 5 s and the match report on match end |

Not autoloads: `Main.cs`/`Main.tscn` (the game scene), `KillfeedUI`, `DebugOverlay`, `LobbyApi.cs`
(the menu's static HTTP client), `InputBindings.cs` (rebinding: `[bindings_keyboard]` and
`[bindings_controller]` in settings.cfg over project.godot's defaults), and `GameSettings.cs` (`user://settings.cfg`: always
load → change → save, so no section wipes another; display mode is applied at menu start and
skipped when headless or movie-writing).

## Rules

- **Autoloads outlive scenes.** A player goes menu → room → menu → practice without restarting,
  so any per-session state must be reset:
  - `MatchServer.BeginSession()` and `EndSession()`, called from `Main._Ready` / `_ExitTree`;
  - `Main`'s statics are cleared in `_ExitTree`.
  Anything new that is scoped to a scene needs the same treatment.
- **Read `Net` state live** (`Net.Instance.IsServer` etc.). Never cache it in `_Ready`: the mode
  changes after boot.
- `Main._Ready` calls `Net.BeginGameScene()`, which connects a join the menu queued. Clients must
  connect **after** `Main` exists, because the MultiplayerSpawner replicates players as soon as a
  peer connects.
- Server-only logic: guard with `Net.Instance.IsServer` (or `_isAuthoritative` in MatchServer,
  which includes offline practice).
- Broadcasts from autoloads work like per-node ones, because autoload NodePaths are identical
  on every peer.
- Don't send RPCs while a client is still connecting (check `GetConnectionStatus() == Connected`).
- `LobbyApi` records mirror `services/lobby/Models.cs`. **Change both together.**
- `RoomReporter` authenticates with `--room-token`. Never log the token.
