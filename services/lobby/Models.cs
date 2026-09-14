namespace TheRoom.Lobby;

// ---- Public API (/api) ----

public sealed record CreateRoomRequest(string? Name, string? Mode, bool IsPrivate);

public sealed record RoomView(
    string Code, string Name, string Mode, bool IsPrivate, int Players, int MaxPlayers,
    string State, string Host, int Port, DateTime CreatedAt);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalItems, int TotalPages);

public sealed record PlayerLine(int Rank, string Name, string Character, int Score, int Kills, int Deaths);

public sealed record MatchSummary(
    long Id, string RoomCode, string RoomName, string Mode, DateTime EndedAt, double DurationSeconds,
    string? MvpName, int MvpScore, int PlayerCount, IReadOnlyList<PlayerLine> TopPlayers);

public sealed record MatchDetail(
    long Id, string RoomCode, string RoomName, string Mode, DateTime EndedAt, double DurationSeconds,
    string? MvpName, int MvpScore, IReadOnlyList<PlayerLine> Players, IReadOnlyList<string> Awards);

public sealed record LeaderboardRow(
    int Rank, string Name, int Matches, int Wins, int Kills, int Deaths, int TotalScore, int BestScore);

public sealed record ErrorBody(string Error);

// ---- Internal API (/internal, loopback only, called by the room's own game server) ----

public sealed record HeartbeatRequest(string Token, int Players, string? State);

public sealed record ReportedPlayer(string Name, string? Character, int Score, int Kills, int Deaths);

public sealed record MatchReport(string Token, double DurationSeconds, List<ReportedPlayer> Players, List<string>? Awards);

public static class Modes
{
    public const string DuelPit = "duelpit";
    public const string Chaos = "chaos";

    /// <summary>GDD deathmatch configs: Duel Pit 6–10 players, Chaos 12–20.</summary>
    public static int MaxPlayers(string mode) => mode == Chaos ? 20 : 10;

    public static bool IsValid(string? mode) => mode is DuelPit or Chaos;
}
