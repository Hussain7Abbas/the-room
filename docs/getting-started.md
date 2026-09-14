# Getting started

## Requirements

| Tool | Version | Notes |
|---|---|---|
| Godot | **4.7.2 .NET (mono)** | The standard (non-.NET) build can't run C#. The Makefile expects `/Applications/Godot_mono.app` (macOS); change `GODOT` at the top of the `Makefile` if yours is elsewhere. |
| .NET SDK | 8 or newer | The game targets `net8.0`. The lobby targets `net8.0` with `RollForward=Major`, so a newer runtime also works. |
| make, bash | any | All common tasks are Makefile targets. |

## First run

```bash
git clone <this repo> the-room && cd the-room
make setup          # restore NuGet packages and build once
make run-client-menu   # the main menu, against a local lobby (start one first, see below)
```

Ways to play locally:

| Goal | Command |
|---|---|
| Open the real menu against the **live** lobby | open the project in Godot and press Play (F5), or run Godot with no flags |
| Local lobby + menu (create and join rooms offline from the internet) | `make run-lobby` in one terminal, `make run-client-menu` in another |
| Quick server + N windowed clients, no menu | `make run-local N=2` |
| Practice alone | menu → **Practice alone**, or run `core/Main.tscn` directly in the editor (F6) |
| Fill a room with bots | `make run-bots N=6 HOST=127.0.0.1 PORT=60010` |
| Preview a character model's animations | `make preview-animations MODEL=res://assets/characters/zain/zain.fbx` |

`make help` lists every target.

## Controls

| Action | Input |
|---|---|
| Move | W A S D |
| Look (camera) | Mouse. Your character faces the way you move, and turns to the camera when you attack. |
| Light attack | Left mouse |
| Heavy attack (lunge) | Right mouse |
| Parry | Q |
| Dash | Shift |
| Jump | Space |
| Ability | E |
| Scoreboard | Tab (hold) |
| Menu (room code, leave) | Esc |

The same list is in the game under **Settings → Controls**. **Settings → Display** switches
between Maximized (the default), Windowed and Fullscreen.

## Command-line flags

Godot's own flags come first; the game's flags go **after a literal `--`**:

```bash
godot --path . -- --connect=room-udp.iscoded.com --port=60010 --name=Ana --character=blink
```

| Flag | Used by | Meaning |
|---|---|---|
| `--server` | server | Run a dedicated headless server. Also implied by a headless display without `--connect`. |
| `--connect=<host>` | client | Join directly, skipping the menu. |
| `--port=<n>` | both | Port to listen on or connect to (default 60010). |
| `--name=<name>` | client | Display name (the menu's name field overrides it). |
| `--character=<id>` | client | `blink` or `firepatch` (see `abilities/CharacterRegistry.cs`). |
| `--bot` | client | Headless AI player (wander, attack, use ability). Quits when its session ends. |
| `--sim-latency=<ms>`, `--sim-loss=<0..1>` | client | Delay or drop this client's own outgoing RPCs (testing only). |
| `--config=duelpit\|chaos` | server | Score target (25 or 40, from tuning). |
| `--max-players=<n>` | server | Player cap (the lobby passes 10 for Duel Pit, 20 for Chaos). |
| `--room-code`, `--room-name`, `--lobby-url`, `--room-token` | server | Set by the lobby when it starts a room. Don't pass them by hand. |
| `--api=<url>` | client | Lobby base URL (default `https://room-api.iscoded.com`). |
| `--menu-page=history\|leaderboard` | client | Open the menu on that page (screenshots, tests). |
| `--menu-open=settings\|controls\|about` | client | Open that dialog on top of the menu (screenshots, tests). |

## Project layout

```
core/        autoloads (Net, MatchServer, CombatServer, …), Main game scene, lobby client
player/      Player.cs: movement, prediction, combat state machine, bots
abilities/   ability grammar framework + character definitions
animation/   CharacterModel + shared humanoid animation set
ui/          main menu, in-game menu, theme (all built in C#)
maps/room/   the arena
tuning/      tuning.tres, every gameplay number
assets/      FBX models and animations
characters/  character spec sheets
services/    lobby (ASP.NET) + its tests
tests/       game tests (GoDotTest)
tools/       dev tools (animation preview)
deploy/      nginx + systemd files
plan/        build plan and change log
docs/        you are here
```
