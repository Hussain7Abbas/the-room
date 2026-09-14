# Audio, effects and graphics

Everything here is cosmetic: it runs on clients only, and headless peers (the dedicated server,
bots) skip it. Nothing in it is networked beyond the cues gameplay already sends.

## Sound (`core/GameAudio.cs`)

`GameAudio` is an autoload. At startup it creates two buses that feed **Master**:

| Bus | What plays on it | Settings → Sound slider |
|---|---|---|
| Master | everything | **Main Volume** (default 80%) |
| Music | the background track, looping from the menu through every match | **Music** (default 40%) |
| Effects | stab, swing, jump, landing, roll, kick, footsteps, death, menu clicks | **Effects** (default 80%) |

Volumes are saved in `[audio]` of `user://settings.cfg`. Sliders map to the bus on a squared
curve, so the lower half is still usable; 0 mutes the bus. The track itself plays at −9 dB so the
music stays quiet under the effects.

Sound effects are files named `<name>_<n>` in `assets/audio/sfx/`; `GameAudio.Play3D(node,
"<name>", position)` picks a random variant and plays it positionally with a little pitch spread.
To add a sound, drop in `mysound_1.ogg` (and `_2`, `_3` for variety) and call it by name.

| Sound | When |
|---|---|
| `stab` | a hit lands (server-confirmed); heavies lower and louder |
| `swing` | a light or heavy attack starts (server cue) |
| `jump` / `land` | leaving / touching the ground; landing volume follows the fall speed |
| `step` | footsteps while moving on the ground, faster when sprinting |
| `roll` | a dodge starts |
| `kick` | the drop kick's feet come down |
| `death` | a player dies |
| `ui_click` | any menu button |

## Effects (`effects/Fx.cs`)

- **Blood:** a landed hit sprays droplets from the **contact point**, where the knife (or the
  drop kick's feet) meets the victim: on the victim's body, on the side facing the attacker, at
  the attacker's striking height (`Player.StrikeContactPoint`; the server works it out from the
  two bodies because it has no knife mesh). The spray goes the way the blow was going, and small
  blood drops appear on the floor where the spray comes down. They are decals, so they follow the
  surface (floor, deck, crate top). Each drop fades out in its last second and is **gone 3 s after
  the hit**.
- **Dust:** a puff **on the floor** under the player (snapped with a downward ray, so it sits
  on the ground, a deck or a crate top; none in mid-air) when you jump, land (any height; bigger for a bigger fall),
  sprint (a small puff every step), roll (a trail along the roll) and when the drop kick lands.

Jump, landing, footstep and dust cues are worked out by each copy of a player from how it moves,
so a remote player's landing kicks up dust on your screen without any extra network traffic.

## Graphics (Settings → Graphics)

**Texture Quality**: Low, **Mid** (default) or High, saved in `[graphics]` and applied to the
root viewport at launch and immediately on change (`GameSettings.ApplyTextureQuality`).

| Level | Mipmap bias | Anisotropic filtering |
|---|---|---|
| Low | +1.5 (softer textures, less texture bandwidth) | off |
| Mid | 0 | 4× |
| High | −0.25 (a little sharper) | 16× |

The arena's materials use the anisotropic filter so the filtering level takes effect. Low does
not reduce how much video memory textures take; it samples smaller versions of them.

## Sources

All CC0; see [CREDITS.md](../CREDITS.md).
