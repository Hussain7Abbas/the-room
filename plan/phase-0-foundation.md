# Phase 0 — Project Foundation

**Goal:** a skeleton where one command launches a headless server and N clients locally, and every gameplay number already has a home in the tuning file.

**Gate:** a new team member can clone → open in Godot 4.7 → press one button (or `make run-local`) and see two capsules in the same room.

## Tasks
- [x] Git — already initialized by the user (`origin` = `github.com/Hussain7Abbas/the-room`).
- [x] Language confirmed: **C#**. `.NET SDK 10.0.401` at `/usr/local/share/dotnet` (not on default PATH — Makefile adds it). Godot.NET.Sdk pinned to `4.7.2`, TFM `net8.0`. `The Room.sln` / `The Room.csproj` created and building clean.
- [x] Folder layout (created so far; `combat/`, `abilities/`, `characters/`, `match/`, `ui/` are still empty — Phase 2/3 territory):
  ```
  res://
    core/         # Net.cs, Events.cs, TuningService.cs, Main.cs + Main.tscn  ✅
    player/       # Player.cs + Player.tscn (movement + 3rd-person camera)    ✅
    maps/room/    # Room.tscn (grey-box arena)                                ✅
    tuning/       # Tuning.cs + tuning.tres                                   ✅
    combat/ abilities/ characters/ match/ ui/  — not created yet, Phase 2/3
  ```
- [x] `Tuning` custom `Resource` subclass (`tuning/Tuning.cs`) + `tuning/tuning.tres` with every number from GDD §5 pre-filled.
- [x] Autoloads registered in `project.godot`: `Net`, `Tuning` (→ `TuningService.cs`), `Events`.
- [x] Command-line boot implemented in `core/Net.cs`: `--server`, `--connect=<ip>`, `--port=<port>`, `--name=<name>`; headless auto-detected via `DisplayServer.GetName() == "headless"` **unless** `--connect` is also given (needed for headless bot clients from Phase 1 on). No flags → fully offline single-player mode.
  - ⚠️ Found & fixed during testing: `OS.GetCmdlineArgs()` does **not** return user args after `--`; must use `OS.GetCmdlineUserArgs()`. Verified with a real two-process ENet server+client handshake (headless, ports 60010+).
- [x] Input map added to `project.godot`: move (WASD), light (LMB), heavy (RMB), parry (Q), dash (Space), ability (E), scoreboard (Tab). Aim is mouse-look (always active); `ui_cancel` (Esc, built-in) toggles mouse capture.
- [x] Grey-box room (`maps/room/Room.tscn`): 40×40 floor, 4 walls, centre plinth (Golden Knife spot), 3 pillars (broken sightlines), one ramp + platform (verticality test), 6 perimeter spawn markers, directional light + environment.
- [x] Third-person camera rig: `SpringArm3D` (pitch, mouse-look, clamped -60°/+70°) under a `CameraPivot`, body yaws with mouse X. Grey-box only — readability/distance tuning happens in Phase 2 at real player counts.
- [x] `Main.tscn`/`Main.cs`: spawns a `Player` per connected peer via `MultiplayerSpawner` (auto-replicates to clients, no manual spawn RPCs needed), cycling through the room's spawn markers. Works offline (spawns local peer 1 immediately) and networked.
- [x] Makefile (via `makefile-standards`): `help`, `install`, `setup`, `build`, `run-server`, `run-client`, `run-local N=<count>`, `clean`, `test` (stub), `export-server`/`export-client`. Verified: `make -n help`, `make help`, `make build`, `make run-server` all run clean.
  - `export-server`/`export-client` were originally stubs (no `export_presets.cfg`); finished later (see `plan/main.md`'s changelog) — real Linux/Windows export templates installed, `export_presets.cfg` hand-written and validated by actually exporting, and the resulting standalone Linux server binary was uploaded and run on the real VPS with a real client connecting to it, not just tested via `godot --path .` from source.
- [ ] **Test framework — deferred.** gdUnit4 (C# support) needs both an editor addon and a NuGet package; GoDotTest is NuGet-only but still needs a Godot-aware host to run against `GodotSharp`. Neither is a small add given the current scope — picking this up properly belongs in Phase 1 alongside the debug/telemetry tooling, rather than wiring something half-usable now. `make test` exists as a stub that says so.

## Verification done
- `dotnet build "The Room.sln"` — 0 errors, 0 warnings.
- Headless run (`--headless`) loads `Main.tscn`, `TuningService` loads `tuning.tres`, `Net` starts a server — no console errors.
- Two-process test: one headless `--server` + one headless `--connect` instance completed a real ENet handshake (`[Net] Peer connected: <id>` on the server, `[Net] Connected to ... as peer <id>` on the client).
- `make help` renders correctly with colorized, grouped sections.

## Deliverables
Runnable project, tuning file, Makefile, folder structure, README with run commands. **All done** except the test framework (see above — carried to Phase 1).

## Out of scope
Any combat, any art, any ability. (Player can currently only walk and look around — no verbs yet.)
