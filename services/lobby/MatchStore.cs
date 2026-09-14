using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace TheRoom.Lobby;

/// <summary>
/// Match history and the season leaderboard, in one SQLite file. Every finished match in any
/// room is written here by its game server (through /internal/rooms/{code}/matches). The leaderboard
/// is computed from these rows rather than stored, so it can't drift from the history.
/// Players are keyed by display name, since there are no accounts (same simplification as the
/// old core/SeasonStats.cs).
/// </summary>
public sealed class MatchStore
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 50;

    private readonly string _connectionString;

    public MatchStore(LobbySettings settings)
    {
        _connectionString = new SqliteConnectionStringBuilder { DataSource = settings.DbPath }.ToString();
        Migrate();
    }

    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private void Migrate()
    {
        using var db = Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            PRAGMA journal_mode = WAL;
            CREATE TABLE IF NOT EXISTS matches (
                id               INTEGER PRIMARY KEY AUTOINCREMENT,
                room_code        TEXT    NOT NULL,
                room_name        TEXT    NOT NULL,
                mode             TEXT    NOT NULL,
                ended_at         TEXT    NOT NULL,
                duration_seconds REAL    NOT NULL,
                mvp_name         TEXT,
                mvp_score        INTEGER NOT NULL DEFAULT 0,
                awards_json      TEXT    NOT NULL DEFAULT '[]'
            );
            CREATE TABLE IF NOT EXISTS match_players (
                match_id  INTEGER NOT NULL REFERENCES matches(id) ON DELETE CASCADE,
                rank      INTEGER NOT NULL,
                name      TEXT    NOT NULL,
                character TEXT    NOT NULL DEFAULT '',
                score     INTEGER NOT NULL,
                kills     INTEGER NOT NULL,
                deaths    INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_matches_ended_at ON matches(ended_at DESC);
            CREATE INDEX IF NOT EXISTS ix_match_players_match ON match_players(match_id);
            CREATE INDEX IF NOT EXISTS ix_match_players_name ON match_players(name COLLATE NOCASE);
            """;
        cmd.ExecuteNonQuery();
    }

    public static (int Page, int PageSize) ClampPaging(int? page, int? pageSize) =>
        (Math.Max(1, page ?? 1), Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize));

    public static int TotalPages(int totalItems, int pageSize) =>
        Math.Max(1, (int)Math.Ceiling(totalItems / (double)pageSize));

    public long Insert(string roomCode, string roomName, string mode, MatchReport report)
    {
        // Rank by score, with ties sharing a rank (two players on 10 are both #1), so "wins"
        // counts every co-winner.
        var players = report.Players
            .Select(p => p with { Name = Clean(p.Name, 32), Character = Clean(p.Character ?? "", 32) })
            .Where(p => p.Name.Length > 0)
            .OrderByDescending(p => p.Score)
            .ThenBy(p => p.Name)
            .ToList();
        // A match nobody scored in has no winner: otherwise every 0–0 match hands everyone a win.
        var mvp = players.FirstOrDefault(p => p.Score > 0);

        using var db = Open();
        using var tx = db.BeginTransaction();

        using var insertMatch = db.CreateCommand();
        insertMatch.Transaction = tx;
        insertMatch.CommandText = """
            INSERT INTO matches (room_code, room_name, mode, ended_at, duration_seconds, mvp_name, mvp_score, awards_json)
            VALUES ($code, $name, $mode, $ended, $duration, $mvp, $mvpScore, $awards)
            RETURNING id;
            """;
        insertMatch.Parameters.AddWithValue("$code", roomCode);
        insertMatch.Parameters.AddWithValue("$name", roomName);
        insertMatch.Parameters.AddWithValue("$mode", mode);
        insertMatch.Parameters.AddWithValue("$ended", DateTime.UtcNow.ToString("o"));
        insertMatch.Parameters.AddWithValue("$duration", Math.Max(0, report.DurationSeconds));
        insertMatch.Parameters.AddWithValue("$mvp", (object?)mvp?.Name ?? DBNull.Value);
        insertMatch.Parameters.AddWithValue("$mvpScore", mvp?.Score ?? 0);
        insertMatch.Parameters.AddWithValue("$awards", JsonSerializer.Serialize(
            (report.Awards ?? new()).Select(a => Clean(a, 120)).Where(a => a.Length > 0).Take(16)));
        var matchId = (long)insertMatch.ExecuteScalar()!;

        foreach (var p in players)
        {
            using var insertPlayer = db.CreateCommand();
            insertPlayer.Transaction = tx;
            insertPlayer.CommandText = """
                INSERT INTO match_players (match_id, rank, name, character, score, kills, deaths)
                VALUES ($match, $rank, $name, $character, $score, $kills, $deaths);
                """;
            insertPlayer.Parameters.AddWithValue("$match", matchId);
            insertPlayer.Parameters.AddWithValue("$rank", 1 + players.Count(o => o.Score > p.Score));
            insertPlayer.Parameters.AddWithValue("$name", p.Name);
            insertPlayer.Parameters.AddWithValue("$character", p.Character ?? "");
            insertPlayer.Parameters.AddWithValue("$score", p.Score);
            insertPlayer.Parameters.AddWithValue("$kills", Math.Max(0, p.Kills));
            insertPlayer.Parameters.AddWithValue("$deaths", Math.Max(0, p.Deaths));
            insertPlayer.ExecuteNonQuery();
        }

        tx.Commit();
        return matchId;
    }

    public PagedResult<MatchSummary> ListMatches(int? page, int? pageSize, string? player)
    {
        var (p, size) = ClampPaging(page, pageSize);
        player = string.IsNullOrWhiteSpace(player) ? null : player.Trim();
        const string filter = "($player IS NULL OR id IN (SELECT match_id FROM match_players WHERE name = $player COLLATE NOCASE))";

        using var db = Open();

        using var count = db.CreateCommand();
        count.CommandText = $"SELECT COUNT(*) FROM matches WHERE {filter};";
        count.Parameters.AddWithValue("$player", (object?)player ?? DBNull.Value);
        var total = Convert.ToInt32(count.ExecuteScalar());

        using var select = db.CreateCommand();
        select.CommandText = $"""
            SELECT id, room_code, room_name, mode, ended_at, duration_seconds, mvp_name, mvp_score,
                   (SELECT COUNT(*) FROM match_players mp WHERE mp.match_id = matches.id)
            FROM matches WHERE {filter}
            ORDER BY ended_at DESC, id DESC
            LIMIT $limit OFFSET $offset;
            """;
        select.Parameters.AddWithValue("$player", (object?)player ?? DBNull.Value);
        select.Parameters.AddWithValue("$limit", size);
        select.Parameters.AddWithValue("$offset", (p - 1) * size);

        var rows = new List<(long Id, string Code, string Name, string Mode, DateTime Ended, double Duration, string? Mvp, int MvpScore, int Count)>();
        using (var reader = select.ExecuteReader())
        {
            while (reader.Read())
            {
                rows.Add((reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    DateTime.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    reader.GetDouble(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetInt32(7), reader.GetInt32(8)));
            }
        }

        var players = LoadPlayers(db, rows.Select(r => r.Id).ToList());
        var items = rows.Select(r => new MatchSummary(r.Id, r.Code, r.Name, r.Mode, r.Ended, r.Duration, r.Mvp, r.MvpScore, r.Count,
            players.GetValueOrDefault(r.Id, new()).Take(3).ToList())).ToList();

        return new PagedResult<MatchSummary>(items, p, size, total, TotalPages(total, size));
    }

    public MatchDetail? GetMatch(long id)
    {
        using var db = Open();
        using var select = db.CreateCommand();
        select.CommandText = """
            SELECT id, room_code, room_name, mode, ended_at, duration_seconds, mvp_name, mvp_score, awards_json
            FROM matches WHERE id = $id;
            """;
        select.Parameters.AddWithValue("$id", id);
        using var reader = select.ExecuteReader();
        if (!reader.Read())
            return null;

        var awards = JsonSerializer.Deserialize<List<string>>(reader.GetString(8)) ?? new();
        var ended = DateTime.Parse(reader.GetString(4), null, System.Globalization.DateTimeStyles.RoundtripKind);
        var detail = new MatchDetail(reader.GetInt64(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
            ended, reader.GetDouble(5), reader.IsDBNull(6) ? null : reader.GetString(6), reader.GetInt32(7),
            LoadPlayers(db, new() { id }).GetValueOrDefault(id, new()), awards);
        return detail;
    }

    public PagedResult<LeaderboardRow> Leaderboard(int? page, int? pageSize)
    {
        var (p, size) = ClampPaging(page, pageSize);
        using var db = Open();

        using var count = db.CreateCommand();
        count.CommandText = "SELECT COUNT(DISTINCT name COLLATE NOCASE) FROM match_players;";
        var total = Convert.ToInt32(count.ExecuteScalar());

        using var select = db.CreateCommand();
        select.CommandText = """
            SELECT MIN(name), COUNT(*), SUM(rank = 1 AND score > 0), SUM(kills), SUM(deaths), SUM(score), MAX(score)
            FROM match_players
            GROUP BY name COLLATE NOCASE
            ORDER BY SUM(rank = 1 AND score > 0) DESC, SUM(score) DESC, MIN(name)
            LIMIT $limit OFFSET $offset;
            """;
        select.Parameters.AddWithValue("$limit", size);
        select.Parameters.AddWithValue("$offset", (p - 1) * size);

        var items = new List<LeaderboardRow>();
        using var reader = select.ExecuteReader();
        var rank = (p - 1) * size;
        while (reader.Read())
        {
            items.Add(new LeaderboardRow(++rank, reader.GetString(0), reader.GetInt32(1), reader.GetInt32(2),
                reader.GetInt32(3), reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6)));
        }

        return new PagedResult<LeaderboardRow>(items, p, size, total, TotalPages(total, size));
    }

    private static Dictionary<long, List<PlayerLine>> LoadPlayers(SqliteConnection db, List<long> matchIds)
    {
        var result = new Dictionary<long, List<PlayerLine>>();
        if (matchIds.Count == 0)
            return result;

        using var select = db.CreateCommand();
        var names = matchIds.Select((_, i) => $"$m{i}").ToList();
        select.CommandText = $"""
            SELECT match_id, rank, name, character, score, kills, deaths FROM match_players
            WHERE match_id IN ({string.Join(",", names)})
            ORDER BY match_id, rank, name;
            """;
        for (var i = 0; i < matchIds.Count; i++)
            select.Parameters.AddWithValue(names[i], matchIds[i]);

        using var reader = select.ExecuteReader();
        while (reader.Read())
        {
            var id = reader.GetInt64(0);
            if (!result.TryGetValue(id, out var list))
                result[id] = list = new();
            list.Add(new PlayerLine(reader.GetInt32(1), reader.GetString(2), reader.GetString(3),
                reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6)));
        }
        return result;
    }

    /// <summary>Trims, drops control characters and caps length. Used for every string a game
    /// server or player sends us.</summary>
    public static string Clean(string value, int maxLength)
    {
        var chars = value.Where(c => !char.IsControl(c)).ToArray();
        var cleaned = string.Join(' ', new string(chars).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        return cleaned.Length > maxLength ? cleaned[..maxLength].TrimEnd() : cleaned;
    }
}
