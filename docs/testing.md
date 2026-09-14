# Testing

## Automated tests

```bash
make test        # both suites below, and both must pass before a commit
make lobby-test  # just the lobby's
```

| Suite | Framework | Location | Covers |
|---|---|---|---|
| Game | Chickensoft GoDotTest + Shouldly (runs inside Godot, headless) | `tests/` | ability grammar validator, tuning lookup fallback, camera rig orientation, animation retarget contract (every clip's bones exist on every model), root-motion stripping |
| Lobby | xUnit | `services/Lobby.Tests/` | match storage, tied ranks, no-winner rule, pagination and clamping, player filter, leaderboard order, input cleaning |

The game suite runs `tests/TestRunner.tscn` with `--run-tests --quit-on-finish` and exits with code
1 on any failure.

## Bots

A bot is a headless client that wanders, attacks, parries and uses its ability:

```bash
make run-server                               # terminal 1
make run-bots N=6 HOST=127.0.0.1 PORT=60010   # terminal 2
```

The server log prints `[Combat] X killed Y (method)`, `[Match]` state changes and `[Physics]` rescues.
Bots quit when their session ends.

Network conditions: add `--sim-latency=120 --sim-loss=0.05` to a client's flags.

**Watching whole matches quickly:** temporarily set `MatchTimeLimitSeconds = 25`,
`LastCallTimeRemainingSeconds = 8` and `ResultsScreenDurationSeconds = 5` in `tuning/tuning.tres`.
**Put them back before committing.**

## Lobby end to end

```bash
make run-lobby                                   # starts MAIN on 60410
curl -s localhost:5310/api/rooms
curl -s -X POST localhost:5310/api/rooms -H 'Content-Type: application/json' \
     -d '{"name":"Test","mode":"duelpit","isPrivate":true}'
godot --headless --path . -- --connect=127.0.0.1 --port=60410 --bot   # heartbeat shows players: 1
curl -s 'localhost:5310/api/matches?page=1&pageSize=5'
```

## Looking at the game

Headless logs can't show whether things **look** right. Several real bugs in this project were only
visible in rendered frames: reversed camera, missing name labels, players sinking into the floor,
menu text pushed off-screen. Godot's **movie writer** renders frames to PNG without anyone at the
keyboard:

```bash
# a real client watching a local match (start a server and some bots first)
godot --path . --write-movie /tmp/frames/f.png --fixed-fps 30 --quit-after 300 \
      -- --connect=127.0.0.1 --port=60010 --bot

# a menu page (against a local lobby)
godot --path . --write-movie /tmp/menu/f.png --fixed-fps 30 --quit-after 300 \
      -- --api=http://127.0.0.1:5310 --menu-page=history

# a character's animations
godot --path . res://tools/AnimationPreview.tscn --write-movie /tmp/anim/f.png --fixed-fps 30 --quit-after 170
```

Notes:

- The movie writer runs **faster than real time**. Record enough frames for network and HTTP
  replies to arrive (300 or more), and look at the **last** frame.
- It records at the window size (1152×648); `--resolution` is ignored.
- To see many frames at once, stitch a contact sheet with Godot's `Image.blit_rect` in a small
  `--script`. That needs no PIL or ImageMagick.

## Before committing

1. `make test` passes.
2. For gameplay, network or match changes: bots ran against a server, with no errors in the logs.
3. For anything visual: frames rendered and looked at.
4. Temporary diagnostics removed, and `tuning.tres` restored.
5. Docs and CLAUDE.md updated.
6. The staged diff doesn't contain the server's IP address.
