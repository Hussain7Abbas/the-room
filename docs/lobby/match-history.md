# Match history & leaderboard

## Where results come from

At the end of every match the room's game server (`MatchServer.EndMatch` → `RoomReporter`) posts
one report to the lobby. The report lists every player who took part, including anyone who
scored and then left, with name, character, score, kills and deaths, plus the match's awards. If
the lobby can't be reached, it retries once after 2 s.

Practice matches and servers not started by the lobby don't report.

## Storage (`services/lobby/MatchStore.cs`)

One SQLite file (`LOBBY_DB`) in WAL mode:

```sql
matches(id, room_code, room_name, mode, ended_at, duration_seconds, mvp_name, mvp_score, awards_json)
match_players(match_id, rank, name, character, score, kills, deaths)
```

- The schema is created in `Migrate()` with `CREATE … IF NOT EXISTS`. To evolve it, add guarded
  `ALTER TABLE` statements; never drop data.
- Indexes: `matches(ended_at)`, `match_players(match_id)`, `match_players(name COLLATE NOCASE)`.
- Every string from a game server is cleaned (control characters stripped, length capped).

## Rules

- **Rank:** by score, and **ties share a rank**: two players on 10 are both #1, the next is #3.
- **Winner / MVP:** the top score, but **only if it's above 0**. A match nobody scored in has no
  winner. Otherwise every 0–0 match would hand everyone a win.
- **Leaderboard:** grouped by display name (case-insensitive), ranked by wins, then total score,
  then name. There are no accounts, so two people using the same name share one row.

## Pagination

Both lists are paginated the same way (`page`, `pageSize` → `items`, `page`, `pageSize`,
`totalItems`, `totalPages`):

- `page` below 1 becomes 1, and `pageSize` is clamped to 1–50;
- a page past the end returns empty `items`, with correct totals.

The menu's `ui/PaginationBar` shows "Page X of Y · N matches" with first, previous, next and last
buttons and a 10/20/50 per-page picker. It always displays the page the server returned.

## Tests

`services/Lobby.Tests/MatchStoreTests.cs` covers:

- tied ranks;
- the no-winner rule;
- newest-first paging and totals;
- clamping;
- the case-insensitive player filter;
- leaderboard order and paging;
- string cleaning.

## Backups

The whole history is the one file `LOBBY_DB`. See [Server configuration](../server-config.md#backups).
