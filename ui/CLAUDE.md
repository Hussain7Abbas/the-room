# ui/ — menus

> **Keep this file in sync** with the code in `ui/`. When a screen, flow or theme rule changes,
> update this file and `docs/ui.md` in the same change.

## Files

- `MainMenu.cs` / `.tscn` is the **project main scene**:
  - name and character picker, saved in `user://settings.cfg`;
  - Play: room list, join by code, create room;
  - Match history: paginated, with a details panel;
  - Leaderboard: paginated;
  - Practice and Quit.
  - `--menu-page=history|leaderboard` opens a specific page, and `--menu-open=settings|controls|about`
    opens a dialog (used for screenshots and tests).
- `GameMenu.cs` is added to `core/Main.tscn`, clients only. It shows the "Connecting…" overlay
  (12 s timeout) and the Esc menu (room name and code, Resume, Leave). It exposes `GameMenu.IsOpen`.
- `UiTheme.cs`: the single theme (colours, styleboxes, type variations `AccentButton`,
  `NavButton`, `GhostButton`, `Card`, `Muted`, `Heading`, `Title`) and small builders.
- `ModalDialog.cs`: base popup (dim background, title, Close, Esc closes it first).
  `SettingsDialog.cs` has four tabs: Display (display mode), Graphics (texture quality,
  `GameSettings.ApplyTextureQuality`), Sound (Main/Music/Effects sliders, `core/GameAudio`) and
  Controls: the bindings of
  the device in use (`core/InputDevices`), rebindable (`core/InputBindings`), rebuilt when the
  device changes. `InputGlyphs.cs` turns a binding into a key cap (layout-aware key name) or a
  controller icon (Kenney CC0 prompts in `assets/ui/input/{xbox,playstation}/`); use it anywhere
  a binding is shown (the HUD's ability badge does). `AboutDialog.cs` has the description,
  © Voidra Team and the org link. Both open from the main menu, and Settings also opens from the
  Esc menu.
- `PlayerHud.cs` + `HudBar.cs` (in `core/Main.tscn`): the top-left HUD with health (and the
  damage chip), stamina and the ability cooldown tile. It reads `Player.HealthFraction`,
  `StaminaFraction`, `IsExhausted` and `AbilityCooldownRemaining/Total`. The top-left is the HUD's;
  the debug overlay lives bottom-left (debug builds, F3).
- `PaginationBar.cs`: reusable pager. It raises `PageRequested(page, size)`; call `SetPage` with
  what the server returned.

## Rules

- UI is built in C# with `UiTheme`. Don't add theme `.tres` files or per-control colour hacks
  when a theme variation fits.
- Every HTTP call goes through `core/LobbyApi` and is `async`. **After each `await`, check
  `IsInsideTree()`**: the player may have left the menu.
- Show failures with `ShowToast(message, UiTheme.Danger)`. Messages must say what happened and
  what to do.
- Disable actions while a request is in flight (`SetBusy`), so double clicks can't race.
- The layout must fit the default 1152×648 window with no horizontal scrollbars. **Render and
  look** after layout changes: movie writer with `--menu-page=…` (see `docs/testing.md`).
- Controller support: while a controller is active, `InputDevices` makes every `Button`
  focusable and keeps something focused, so screens need no per-screen gamepad code. Buttons
  stay `FocusMode.None` for the mouse (no focus rings). Open/close with `pause_menu` (Esc or
  Start), never `ui_cancel` alone: the pad's B is also the dodge.
- Pad navigation (`InputDevices`): the focus is kept inside the top-most `ModalDialog` (it moves
  there when a dialog opens, and a move that would leave it steps in reading order instead);
  `ScrollContainer`s follow the focus; a down/up that goes nowhere scrolls the list. Test with a
  driver that sends `InputEventJoypadButton`s (docs/testing.md).
- The default font lacks many symbols (▶ ★ ●). Stick to Latin plus `• · « » ‹ › —`.
