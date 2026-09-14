# Phase 0 — Project Foundation

**Goal:** a skeleton where one command launches a headless server and N clients locally, and every gameplay number already has a home in the tuning file.

**Gate:** a new team member can clone → open in Godot 4.7 → press one button (or `make run-local`) and see two capsules in the same room.

## Tasks
- [ ] `git init`, extend `.gitignore` (`.godot/`, `export/`, `*.tmp`), first commit.
- [ ] Confirm D1 (language). If C#: install .NET 8 SDK and verify build; if GDScript: nothing to install.
- [ ] Folder layout:
  ```
  res://
    core/         # net, match state, tuning loader, autoloads
    combat/       # melee verbs, hit resolution, health
    player/       # player scene, controller, camera, input
    abilities/    # ability base class + one folder per character
    characters/   # one folder per developer (owned by them)
    match/        # bounty, golden knife, last call, scoring, respawn
    ui/           # HUD, killfeed, scoreboard, awards
    maps/room/    # the arena
    tuning/       # tuning.tres + Tuning.gd resource class
    tests/        # GUT or gdUnit4 tests
  ```
- [ ] `Tuning` custom `Resource` class + `tuning.tres` with every number from GDD §5 pre-filled (light 0.12s/35%, heavy 0.40s/65%/3m, parry 0.15s window/3s CD/0.5s stagger, dash ~4s CD, execute lock 0.6s, respawn 1.5s/1.0s, spawn protection 1.5s, golden knife 45s/30s/20s, score 25/8min, chaos 40, Last Call 75%).
- [ ] Autoloads: `Net` (host/join/server bootstrap), `Tuning` (loads the .tres), `Events` (signal bus).
- [ ] Command-line boot: `--server`, `--connect <ip>`, `--name <player>`; headless detection via `DisplayServer.get_name() == "headless"`.
- [ ] Input map: move (WASD), aim (mouse), light (LMB), heavy (RMB), parry (Q / MMB), dash (Space), ability (E), scoreboard (Tab).
- [ ] Grey-box room: floor, walls, a centre plinth, 2–3 pillars (broken sightlines), one ramp (verticality). CSG only.
- [ ] Makefile (via `makefile-standards`): `run-server`, `run-client`, `run-local N=4`, `export-server` (Linux headless), `export-client` (Windows), `test`.
- [ ] Test framework installed (gdUnit4 or GUT) with one passing test.

## Deliverables
Runnable project, tuning file, Makefile, empty-but-correct folder structure, README with the run commands.

## Out of scope
Any combat, any art, any ability.
