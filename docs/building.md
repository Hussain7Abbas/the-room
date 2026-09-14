# Building the game

Builds are Godot exports defined in [`export_presets.cfg`](../export_presets.cfg). Every one
runs from the command line through `make`, and everything lands under `build/`, which git
ignores.

| Target | Command | Output | Preset |
|---|---|---|---|
| **macOS app** (Apple Silicon + Intel) | `make export-mac` | `build/macos/The Room.app` | `mac` |
| **Windows client** (x86_64) | `make export-client` | `build/client/the-room.exe` (+ `.pck` and a `data_…` folder next to it) | `client` |
| **Linux dedicated server** (headless) | `make export-server` | `build/server/the-room-server.x86_64` | `server` |

Each `make export-*` first runs `make build`, so the C# is always compiled fresh.

**Requirements:** Godot **4.7.2 .NET** export templates. Install them from the editor with
**Editor → Manage Export Templates → Download and Install**; they end up in
`~/Library/Application Support/Godot/export_templates/4.7.2.stable.mono/`. The macOS export also
needs Apple's `codesign`, which comes with every Mac (Xcode Command Line Tools).

## macOS

```bash
make export-mac
open "build/macos/The Room.app"
```

- **Universal:** one app runs natively on Apple Silicon and Intel Macs. It's about 330 MB, mostly
  a .NET runtime for each architecture; the game itself is about 6 MB.
- **Signing:** ad-hoc, done by Apple's `codesign` (preset option *Code Signing → Xcode codesign*).
  - Godot's *built-in* signer produced an app that Apple Silicon killed at launch (exit 137, no
    output), so don't switch back to it.
  - The .NET runtime needs the entitlements JIT, unsigned executable memory and disable library
    validation. They're already set in the preset.
- **Textures:** Apple Silicon needs ETC2/ASTC textures, so the project imports them
  (`rendering/textures/vram_compression/import_etc2_astc=true`). Without it the export refuses to
  run.

### Sharing the Mac app

The app isn't notarized; that needs a paid Apple Developer account. On **your own Mac** it opens
normally. When someone **downloads** it, macOS quarantines it and says it "can't be opened" or is
"damaged". They can do either of these:

- Right-click the app → **Open** → **Open**, once; or
- run `xattr -dr com.apple.quarantine "/Applications/The Room.app"` in Terminal.

Zip it before sending (`ditto -c -k --keepParent "build/macos/The Room.app" TheRoom-mac.zip`), so
the bundle survives upload tools.

To remove the warning for everyone, sign with a **Developer ID** certificate and notarize:

- in the `mac` preset, set *Code Signing → Identity* and *Apple Team ID*;
- set *Notarization* to your Apple ID or API key;
- keep the entitlements.

## Windows

```bash
make export-client
```

Ship the whole `build/client/` folder: the `.exe` loads the `.pck` and the `data_The Room_…`
folder beside it. The build isn't code-signed, so Windows SmartScreen shows "Windows protected
your PC". Click **More info → Run anyway**.

## Which server do builds connect to?

All client builds use the live lobby at `https://room-api.iscoded.com` (`Net.DefaultApiBaseUrl` in
`core/Net.cs`). Players on any platform can play together. To point a build at another server,
change that constant and rebuild, or launch with `-- --api=https://…`.
