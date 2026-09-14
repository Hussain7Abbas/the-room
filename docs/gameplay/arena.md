# Arena

The one map: a walled **60 × 60 m** yard (1.5× the old 40 m grey-box room each way) floating
above the clouds. It lives in `maps/arena/Arena.tscn`, which is **generated** by
`tools/build_arena.gd`. Change the generator, then re-run it:

```bash
/Applications/Godot_mono.app/Contents/MacOS/Godot --headless --path . --script res://tools/build_arena.gd
```

The output is a normal scene, so you can open it in the editor, but hand edits are lost on the
next run.

## Layout

Top-down, +x east, +z south:

| Zone | What's there |
|---|---|
| Centre plaza | Cobblestone disc, the Golden Knife plinth (knife at `0, 1.5, 0`), a ring of 6 barriers, 4 street lamps |
| 4 avenues | 9 m lanes from the plaza to the walls, with alternating barriers |
| NE crate yard | Shipping containers (one stacked pair) and stacked wooden crates forming alleys |
| NW barrier maze | Three zig-zag barrier rows with gaps, utility boxes as posts, a container landmark |
| SW boulder field | An L-shaped concrete wall and scattered boulders |
| SE depot | A 2.2 m metal deck with two ramps, a container, barrel clusters |
| Perimeter lane | A 4 m clear lane inside the walls holding the 16 spawn points, all facing the centre |

The arena stores its half-size as metadata (`half_extent`). `Main.ArenaHalfExtent` reads it, and
bots wander inside it.

## Props and collision

Props are CC0 models from Poly Haven (see [CREDITS.md](../../CREDITS.md)), in
`assets/props/polyhaven/`. Every prop gets a **box collider** sized from its measured bounds,
never a mesh collider, so the lunge, dodge and drop-kick sweeps stay predictable. Containers,
walls and decks are textured boxes.

The dedicated server needs the props imported. `make deploy-server` runs `godot --import` on
the box (see [Deployment](../deployment.md)).
