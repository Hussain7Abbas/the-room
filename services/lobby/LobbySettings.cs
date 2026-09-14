namespace TheRoom.Lobby;

/// <summary>All configuration comes from environment variables (set in the systemd unit on the
/// VPS, or by `make run-lobby` locally). Nothing secret lives here: room tokens are generated per
/// room at runtime.</summary>
public sealed record LobbySettings
{
    /// <summary>Loopback only. nginx proxies /api/ to it, and /internal/ is never proxied.</summary>
    public string ListenUrl { get; init; } = "http://127.0.0.1:5310";
    public string GodotBin { get; init; } = "";
    public string GamePath { get; init; } = "";
    public string DbPath { get; init; } = "lobby.db";
    /// <summary>Hostname clients connect to over UDP. On the VPS it's the DNS-only room-udp record,
    /// because Cloudflare's proxy can't carry raw UDP.</summary>
    public string PublicHost { get; init; } = "127.0.0.1";
    public int RoomPortMin { get; init; } = 60011;
    public int RoomPortMax { get; init; } = 60030;
    /// <summary>Player-created rooms that can run at once. Each is a Godot server of about 210 MB.</summary>
    public int MaxRooms { get; init; } = 4;
    /// <summary>The permanent public room, never idle-closed. 0 disables it.</summary>
    public int MainRoomPort { get; init; } = 60010;
    public int RoomIdleSeconds { get; init; } = 300;

    public string InternalBaseUrl => ListenUrl.TrimEnd('/');

    public static LobbySettings FromEnvironment()
    {
        static string Str(string key, string fallback) =>
            Environment.GetEnvironmentVariable(key) is { Length: > 0 } v ? v : fallback;
        static int Int(string key, int fallback) =>
            int.TryParse(Environment.GetEnvironmentVariable(key), out var v) ? v : fallback;

        var d = new LobbySettings();
        return d with
        {
            ListenUrl = Str("LOBBY_LISTEN", d.ListenUrl),
            GodotBin = Str("GODOT_BIN", d.GodotBin),
            GamePath = Str("GAME_PATH", d.GamePath),
            DbPath = Str("LOBBY_DB", d.DbPath),
            PublicHost = Str("PUBLIC_HOST", d.PublicHost),
            RoomPortMin = Int("ROOM_PORT_MIN", d.RoomPortMin),
            RoomPortMax = Int("ROOM_PORT_MAX", d.RoomPortMax),
            MaxRooms = Int("MAX_ROOMS", d.MaxRooms),
            MainRoomPort = Int("MAIN_ROOM_PORT", d.MainRoomPort),
            RoomIdleSeconds = Int("ROOM_IDLE_SECONDS", d.RoomIdleSeconds),
        };
    }
}
