using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace TheRoom.Lobby;

/// <summary>
/// Owns every running room. A room is one headless Godot game server process on its own UDP
/// port. Players connect to it directly through room-udp; the lobby never touches gameplay traffic.
/// Rooms report player count and results back over /internal, authenticated with a token that
/// only the lobby and that process know.
/// </summary>
public sealed class RoomManager : BackgroundService
{
    public sealed class Room
    {
        public required string Code { get; init; }
        public required string Name { get; init; }
        public required string Mode { get; init; }
        public required int Port { get; init; }
        public required string Token { get; init; }
        public bool IsPrivate { get; init; }
        public bool Persistent { get; init; }
        public int MaxPlayers => Modes.MaxPlayers(Mode);
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public Process? Process { get; set; }
        public int Players { get; set; }
        public string State { get; set; } = "starting";
        public DateTime LastHeartbeat { get; set; }
        public DateTime LastOccupied { get; set; } = DateTime.UtcNow;
        public int Restarts { get; set; }
    }

    public sealed record CreateResult(RoomView? Room, int Status, string? Error);

    public const string MainRoomCode = "MAIN";
    private const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // no 0/O or 1/I lookalikes
    private static readonly TimeSpan HeartbeatStale = TimeSpan.FromSeconds(30);
    private const int CreatesPerIpPerMinute = 3;

    private readonly LobbySettings _settings;
    private readonly ILogger<RoomManager> _log;
    private readonly ConcurrentDictionary<string, Room> _rooms = new();
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _createsByIp = new();
    private readonly object _createLock = new();

    public RoomManager(LobbySettings settings, ILogger<RoomManager> log)
    {
        _settings = settings;
        _log = log;
    }

    public IEnumerable<RoomView> ListPublic() => _rooms.Values
        .Where(r => !r.IsPrivate)
        .OrderByDescending(r => r.Persistent)
        .ThenByDescending(r => r.Players)
        .ThenBy(r => r.CreatedAt)
        .Select(View);

    public RoomView? Get(string code) =>
        _rooms.TryGetValue(code.Trim().ToUpperInvariant(), out var room) ? View(room) : null;

    public Room? Authorize(string code, string? token)
    {
        if (!_rooms.TryGetValue(code, out var room) || token is null)
            return null;
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(room.Token), Encoding.UTF8.GetBytes(token))
            ? room
            : null;
    }

    public bool Heartbeat(string code, HeartbeatRequest heartbeat)
    {
        var room = Authorize(code, heartbeat.Token);
        if (room is null)
            return false;

        room.Players = Math.Clamp(heartbeat.Players, 0, room.MaxPlayers);
        room.State = MatchStore.Clean(heartbeat.State ?? "playing", 16);
        room.LastHeartbeat = DateTime.UtcNow;
        if (room.Players > 0)
            room.LastOccupied = DateTime.UtcNow;
        return true;
    }

    public CreateResult Create(CreateRoomRequest request, string clientIp)
    {
        var mode = (request.Mode ?? Modes.DuelPit).Trim().ToLowerInvariant();
        if (!Modes.IsValid(mode))
            return new(null, 400, "Mode must be \"duelpit\" or \"chaos\".");

        var name = MatchStore.Clean(request.Name ?? "", 24);
        if (name.Length == 0)
            name = mode == Modes.Chaos ? "Chaos room" : "Duel Pit";

        if (!AllowCreate(clientIp))
            return new(null, 429, "You're creating rooms too fast. Wait a minute and try again.");

        lock (_createLock)
        {
            if (_rooms.Values.Count(r => !r.Persistent) >= _settings.MaxRooms)
                return new(null, 503, "All room slots are in use. Join an existing room or try again in a few minutes.");

            var port = Enumerable.Range(_settings.RoomPortMin, _settings.RoomPortMax - _settings.RoomPortMin + 1)
                .FirstOrDefault(p => _rooms.Values.All(r => r.Port != p));
            if (port == 0)
                return new(null, 503, "No free ports for a new room.");

            var room = new Room
            {
                Code = NewCode(),
                Name = name,
                Mode = mode,
                Port = port,
                Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
                IsPrivate = request.IsPrivate,
            };

            if (!Spawn(room))
                return new(null, 500, "The room's game server failed to start.");

            _rooms[room.Code] = room;
            _log.LogInformation("Room {Code} \"{Name}\" ({Mode}, {Visibility}) created on port {Port} by {Ip}.",
                room.Code, room.Name, room.Mode, room.IsPrivate ? "private" : "public", room.Port, clientIp);
            return new(View(room), 201, null);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_settings.MainRoomPort > 0)
        {
            var main = new Room
            {
                Code = MainRoomCode,
                Name = "The Room",
                Mode = Modes.DuelPit,
                Port = _settings.MainRoomPort,
                Token = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
                Persistent = true,
            };
            if (Spawn(main))
                _rooms[main.Code] = main;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            Reap();
    }

    /// <summary>Restarts a crashed main room, drops crashed player rooms, closes idle ones, and
    /// zeroes the player count of rooms that stopped reporting.</summary>
    private void Reap()
    {
        var now = DateTime.UtcNow;
        foreach (var room in _rooms.Values)
        {
            var exited = room.Process is null || room.Process.HasExited;
            if (exited)
            {
                if (room.Persistent && room.Restarts < 20)
                {
                    room.Restarts++;
                    _log.LogWarning("Main room exited; restarting (attempt {N}).", room.Restarts);
                    Spawn(room);
                }
                else
                {
                    _log.LogInformation("Room {Code} exited; removing.", room.Code);
                    _rooms.TryRemove(room.Code, out _);
                }
                continue;
            }

            if (room.LastHeartbeat != default && now - room.LastHeartbeat > HeartbeatStale)
                room.Players = 0;

            if (!room.Persistent && room.Players == 0 && now - room.LastOccupied > TimeSpan.FromSeconds(_settings.RoomIdleSeconds))
            {
                _log.LogInformation("Room {Code} idle for {Seconds}s; closing.", room.Code, _settings.RoomIdleSeconds);
                Kill(room);
                _rooms.TryRemove(room.Code, out _);
            }
        }
    }

    private bool Spawn(Room room)
    {
        if (string.IsNullOrEmpty(_settings.GodotBin) || string.IsNullOrEmpty(_settings.GamePath))
        {
            _log.LogError("GODOT_BIN and GAME_PATH must be set to start rooms.");
            return false;
        }

        var info = new ProcessStartInfo(_settings.GodotBin)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        // ArgumentList, never a shell string: the room name is player input.
        foreach (var arg in new[]
                 {
                     "--headless", "--path", _settings.GamePath, "--",
                     "--server", $"--port={room.Port}", $"--config={room.Mode}", $"--max-players={room.MaxPlayers}",
                     $"--room-code={room.Code}", $"--room-name={room.Name}",
                     $"--lobby-url={_settings.InternalBaseUrl}", $"--room-token={room.Token}",
                 })
        {
            info.ArgumentList.Add(arg);
        }

        try
        {
            var process = new Process { StartInfo = info, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => { if (e.Data is { Length: > 0 } line) _log.LogInformation("[{Code}] {Line}", room.Code, line); };
            process.ErrorDataReceived += (_, e) => { if (e.Data is { Length: > 0 } line) _log.LogWarning("[{Code}] {Line}", room.Code, line); };
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            room.Process = process;
            room.State = "starting";
            return true;
        }
        catch (Exception e)
        {
            _log.LogError(e, "Failed to start room {Code}.", room.Code);
            return false;
        }
    }

    private void Kill(Room room)
    {
        try
        {
            if (room.Process is { HasExited: false } process)
                process.Kill(entireProcessTree: true);
        }
        catch (Exception e)
        {
            _log.LogWarning(e, "Failed to stop room {Code}.", room.Code);
        }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var room in _rooms.Values)
            Kill(room);
        return base.StopAsync(cancellationToken);
    }

    private bool AllowCreate(string ip)
    {
        var queue = _createsByIp.GetOrAdd(ip, _ => new Queue<DateTime>());
        lock (queue)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-1);
            while (queue.Count > 0 && queue.Peek() < cutoff)
                queue.Dequeue();
            if (queue.Count >= CreatesPerIpPerMinute)
                return false;
            queue.Enqueue(DateTime.UtcNow);
            return true;
        }
    }

    private string NewCode()
    {
        while (true)
        {
            var code = new string(Enumerable.Range(0, 5).Select(_ => CodeAlphabet[RandomNumberGenerator.GetInt32(CodeAlphabet.Length)]).ToArray());
            if (!_rooms.ContainsKey(code))
                return code;
        }
    }

    private RoomView View(Room r) =>
        new(r.Code, r.Name, r.Mode, r.IsPrivate, r.Players, r.MaxPlayers, r.State, _settings.PublicHost, r.Port, r.CreatedAt);
}
