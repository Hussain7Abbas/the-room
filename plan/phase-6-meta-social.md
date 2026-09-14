# Phase 6 — Meta & Social Layer (post-M4)

**Goal:** the things that carry across sessions — status, not power (GDD §4 meta loop, §7).

**Gate question:** *Do people rematch unprompted and re-tell moments afterwards?*

## Tasks
- [x] **Server-side persistence (JSON on the VPS): per-player season stats, award counts.** `core/SeasonStats.cs` (new autoload): matches played, wins, total/best score, and a count per award title won — written to `res://season_stats.json` (`/opt/the-room/app/season_stats.json` on the deployed server) after every match, loaded back on server startup. Keyed by display name, not peer id (no account system exists — an honest simplification, noted in the file's own doc comment: two people sharing a display name would share a record).
  - **Verified**: recorded a real match's data, confirmed a *fresh server process* correctly loads the existing file back (`[SeasonStats] Loaded 1 player record(s)...`) — the actual point of "persistent," not just "written once."
- [x] **Season leaderboard — exposed via HTTP instead of an in-game lobby**, since no lobby exists yet. `room-api.iscoded.com/season` now serves `season_stats.json` directly (nginx `alias`, `deploy/nginx/room-api.iscoded.com.conf`) — real data, not a placeholder anymore.
  - **Found and fixed a real permissions bug while deploying this**: `/opt/the-room` (the parent directory) was `750`, owned by `theroom:theroom` — nginx's `www-data` user couldn't even traverse into it, so `/season` 403'd before nginx got anywhere near the file. Fixed with a minimal `chmod o+x /opt/the-room` (traversal only, no read/listing added — the directory still isn't browsable). Verified end-to-end with a placeholder file (200 OK, correct JSON), then removed it so the real file gets created by an actual match.
- [x] **Results webhook → team chat — built, but deliberately OFF by default.** `SeasonStats.PostResultsWebhook` posts a plain `{"text": "..."}` payload (Slack/Mattermost-compatible; Discord wants `"content"` instead — a one-line change once we know which platform the team uses) to a URL read from the `RESULTS_WEBHOOK_URL` environment variable. **No URL is committed to this repo or guessed anywhere** — it's a silent no-op until that env var is set. To actually enable it: tell me the team's real Discord/Slack/Teams incoming-webhook URL and which platform it is, and I'll add `Environment=RESULTS_WEBHOOK_URL=...` to the systemd unit on the deployed server (never into a committed file).
- [ ] Cosmetic unlocks tied to award milestones — **not built, genuinely blocked.** There's no roster (Phase 5) and no art to make a "knife skin" or "victory pose" out of. The award-*counting* this needs already exists (`SeasonStats`'s per-award-title counts); only the "spend it on a cosmetic" half is missing, and that half needs the art pass Phase 5 is also blocked on.
- [ ] Lobby: banter space, mode/config select, character select — **not built.** A real UI system on its own, bigger than a single pass belongs to; `--config`/`--character` CLI flags cover the *functional* need in the meantime (Phases 3/4).
- [ ] Remaining characters (up to ~20) — **not built, same Pillar 3 reasoning as Phase 5**: this is real developers' work, not more placeholder examples from Claude.

## Explicitly never
XP, stat upgrades, unlockable abilities, currencies, loot boxes, random drops, daily login rewards, real-clock timers (GDD §4, §7, §8).

## Later modes (only once Deathmatch is fun)
Team DM, Last-Man-Standing, Golden Knife Rush.
