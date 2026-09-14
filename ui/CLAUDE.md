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
  - `--menu-page=history|leaderboard` opens a specific page (used for screenshots and tests).
- `GameMenu.cs` is added to `core/Main.tscn`, clients only. It shows the "Connecting…" overlay
  (12 s timeout) and the Esc menu (room name and code, Resume, Leave). It exposes `GameMenu.IsOpen`.
- `UiTheme.cs`: the single theme (colours, styleboxes, type variations `AccentButton`,
  `NavButton`, `GhostButton`, `Card`, `Muted`, `Heading`, `Title`) and small builders.
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
- The default font lacks many symbols (▶ ★ ●). Stick to Latin plus `• · « » ‹ › —`.
