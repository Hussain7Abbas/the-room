using System.Collections.Generic;
using Godot;
using TheRoom.Config;

namespace TheRoom.Core;

/// <summary>
/// Autoload ("Net" in project.godot). Owns the session: which mode this process is in and the
/// ENet connection, plus switching between the main menu and the game scene.
///
/// CLI flags (all after a literal "--"):
///   --server              dedicated headless server (also implied by a headless display without --connect)
///   --connect=&lt;host&gt;      join a server directly, skipping the menu (bots, scripts)
///   --port=&lt;port&gt;         server port / port to connect to (default 60010)
///   --name=&lt;name&gt;         display name (the menu's name field overrides this for humans)
///   --character=&lt;id&gt;      character id (see abilities/CharacterRegistry.cs)
///   --bot                 headless client driven by wander/stab AI (see player/Player.cs)
///   --sim-latency=&lt;ms&gt;, --sim-loss=&lt;0..1&gt;   local network simulation for this client's own RPCs
///   --config=&lt;duelpit|chaos&gt;   server: score target
///   --max-players=&lt;n&gt;    server: player cap (the lobby passes 10 for Duel Pit, 20 for Chaos)
///   --room-code, --room-name, --lobby-url, --room-token   server: set by services/lobby when it
///                         starts a room, so the room can report to it (core/RoomReporter.cs)
///   --api=&lt;url&gt;          client: lobby API base URL (default https://room-api.iscoded.com)
///
/// With no --server/--connect, the game opens the main menu (ui/MainMenu.tscn).
/// </summary>
public partial class Net : Node
{
    public const int DefaultPort = 60010;
    public const int MaxPlayers = 20;
    public const string MenuScenePath = "res://ui/MainMenu.tscn";
    public const string GameScenePath = "res://core/Main.tscn";
    public const string DefaultApiBaseUrl = "https://room-api.iscoded.com";

    public enum SessionMode { None, Offline, Server, Client }

    /// <summary>A join the menu asked for. Main connects once the game scene is loaded,
    /// because the server starts replicating player spawns the moment a client connects.</summary>
    public sealed record PendingJoin(string Host, int Port, string? RoomCode, string? RoomName);

    public static Net Instance { get; private set; } = null!;

    public SessionMode Mode { get; private set; } = SessionMode.None;
    public bool IsServer => Mode == SessionMode.Server;
    public bool IsClient => Mode == SessionMode.Client;
    public bool IsOffline => Mode == SessionMode.Offline;
    public bool IsBot { get; private set; }
    public string LocalPlayerName { get; private set; } = "Player";
    public string? ChosenCharacterId { get; private set; }
    public bool IsChaosConfig { get; private set; }
    public int ServerMaxPlayers { get; private set; } = MaxPlayers;

    public string ApiBaseUrl { get; private set; } = DefaultApiBaseUrl;
    public string? RoomCode { get; private set; }
    public string? RoomName { get; private set; }
    public string? LobbyUrl { get; private set; }
    public string? RoomToken { get; private set; }
    public PendingJoin? Pending { get; private set; }
    /// <summary>Why the last session ended, shown by the menu once. Null after a normal leave.</summary>
    public string? LastDisconnectReason { get; set; }

    /// <summary>Artificial one-way delay (seconds) applied to this client's own outgoing RPCs. 0 = off.</summary>
    public float SimLatencySeconds { get; private set; }
    /// <summary>Fraction [0,1] of this client's own outgoing RPCs to silently drop. 0 = off.</summary>
    public float SimPacketLossFraction { get; private set; }

    public override void _Ready()
    {
        Instance = this;

        // Fixed tick rate for deterministic server simulation / hit-rewind (GDD §4, Tuning.ServerTickRateHz).
        Engine.PhysicsTicksPerSecond = TuningService.Instance.ServerTickRateHz;

        // Grammar check every boot (CHARACTER-SPEC.md Part 3 review gate, mechanical half) —
        // a broken ability should fail loudly at startup, not get discovered mid-playtest.
        AbilityValidator.RunAndPrint();

        // Subscribed once: SceneMultiplayer outlives every peer, so per-connection lambdas
        // would pile up across sessions.
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ConnectedToServer += OnConnectedToServer;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
        Multiplayer.ServerDisconnected += OnServerDisconnected;

        ParseArgsAndStart();
    }

    private void ParseArgsAndStart()
    {
        // Godot's own engine flags (--headless, --path, ...) are consumed before this point.
        // Ours must come after a literal "--" so Godot hands them to us via GetCmdlineUserArgs()
        // instead of trying (and failing) to parse them itself.
        var args = ParseCliArgs(OS.GetCmdlineUserArgs());

        bool headlessDisplay = DisplayServer.GetName() == "headless";
        bool wantsConnect = args.TryGetValue("connect", out var connectHost);
        // Headless implies "run as server" UNLESS --connect was explicitly given (headless
        // bot clients, used from Phase 1 onward, are headless too but should join, not host).
        bool wantsServer = args.ContainsKey("server") || (headlessDisplay && !wantsConnect);

        int port = args.TryGetValue("port", out var portStr) && int.TryParse(portStr, out var parsedPort)
            ? parsedPort
            : DefaultPort;

        LocalPlayerName = args.TryGetValue("name", out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : $"Player{GD.Randi() % 1000}";

        IsBot = args.ContainsKey("bot");
        ChosenCharacterId = args.TryGetValue("character", out var characterId) && !string.IsNullOrWhiteSpace(characterId)
            ? characterId
            : null;
        IsChaosConfig = args.TryGetValue("config", out var configName) && configName.Equals("chaos", System.StringComparison.OrdinalIgnoreCase);

        if (args.TryGetValue("max-players", out var maxStr) && int.TryParse(maxStr, out var max))
            ServerMaxPlayers = Mathf.Clamp(max, 1, MaxPlayers);
        RoomCode = args.GetValueOrDefault("room-code");
        RoomName = args.GetValueOrDefault("room-name");
        LobbyUrl = args.GetValueOrDefault("lobby-url");
        RoomToken = args.GetValueOrDefault("room-token");
        if (args.TryGetValue("api", out var api) && !string.IsNullOrWhiteSpace(api))
            ApiBaseUrl = api.TrimEnd('/');

        if (args.TryGetValue("sim-latency", out var latencyStr) && float.TryParse(latencyStr, out var latencyMs))
            SimLatencySeconds = Mathf.Max(0f, latencyMs) / 1000f;

        if (args.TryGetValue("sim-loss", out var lossStr) && float.TryParse(lossStr, out var lossFrac))
            SimPacketLossFraction = Mathf.Clamp(lossFrac, 0f, 1f);

        if (wantsServer)
        {
            StartServer(port);
        }
        else if (wantsConnect)
        {
            // Scripted join: skip the menu, the same way the menu's Join does it.
            Pending = new PendingJoin(string.IsNullOrWhiteSpace(connectHost) ? "127.0.0.1" : connectHost, port, null, null);
        }
    }

    /// <summary>Name and character picked in the menu, used for the next session.</summary>
    public void Configure(string name, string? characterId)
    {
        if (!string.IsNullOrWhiteSpace(name))
            LocalPlayerName = name.Trim();
        ChosenCharacterId = string.IsNullOrWhiteSpace(characterId) ? null : characterId;
    }

    public void StartServer(int port = DefaultPort)
    {
        var peer = new ENetMultiplayerPeer();
        var err = peer.CreateServer(port, ServerMaxPlayers);
        if (err != Error.Ok)
        {
            GD.PushError($"[Net] Failed to create server on port {port}: {err}");
            // A lobby-started room that can't bind must exit, so the lobby frees its slot.
            GetTree().Quit(1);
            return;
        }

        Multiplayer.MultiplayerPeer = peer;
        Mode = SessionMode.Server;
        GD.Print($"[Net] Server listening on port {port} (max {ServerMaxPlayers} players){(RoomCode is null ? "" : $", room {RoomCode}")}.");
    }

    /// <summary>Menu: join a room. Switches to the game scene, which then connects.</summary>
    public void Join(string host, int port, string? roomCode, string? roomName)
    {
        Pending = new PendingJoin(host, port, roomCode, roomName);
        LastDisconnectReason = null;
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, GameScenePath);
    }

    /// <summary>Menu: practice alone, no server.</summary>
    public void Practice()
    {
        Pending = null;
        Mode = SessionMode.Offline;
        RoomCode = null;
        RoomName = "Practice";
        LastDisconnectReason = null;
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, GameScenePath);
    }

    /// <summary>Main: the game scene is loaded, so a queued join can connect now. Also covers
    /// running Main.tscn straight from the editor, which gets an offline session.</summary>
    public void BeginGameScene()
    {
        if (Pending is { } join)
        {
            Pending = null;
            StartClient(join.Host, join.Port);
            RoomCode = join.RoomCode;
            RoomName = join.RoomName;
        }
        else if (Mode == SessionMode.None)
        {
            Mode = SessionMode.Offline;
        }
    }

    private void StartClient(string host, int port)
    {
        var peer = new ENetMultiplayerPeer();
        var err = peer.CreateClient(host, port);
        if (err != Error.Ok)
        {
            Leave($"Couldn't connect to {host}:{port} ({err}).");
            return;
        }

        Multiplayer.MultiplayerPeer = peer;
        Mode = SessionMode.Client;
        GD.Print($"[Net] Connecting to {host}:{port} ...");
    }

    /// <summary>Ends the session and returns to the main menu. <paramref name="reason"/> is shown
    /// there (null for a normal "Leave room"). Headless bots just quit.</summary>
    public void Leave(string? reason = null)
    {
        if (IsBot)
        {
            GD.Print($"[Net] Session ended{(reason is null ? "" : $": {reason}")}; bot exiting.");
            GetTree().Quit();
            return;
        }

        if (Multiplayer.MultiplayerPeer is ENetMultiplayerPeer enet)
            enet.Close();
        Multiplayer.MultiplayerPeer = new OfflineMultiplayerPeer();

        Mode = SessionMode.None;
        Pending = null;
        RoomCode = null;
        RoomName = null;
        LastDisconnectReason = reason;
        GetTree().CallDeferred(SceneTree.MethodName.ChangeSceneToFile, MenuScenePath);
    }

    /// <summary>
    /// Routes an outgoing RPC call through this client's simulated latency/packet-loss
    /// (--sim-latency / --sim-loss), for testing prediction/reconciliation locally without
    /// a real bad connection. A no-op passthrough when neither flag is set.
    /// </summary>
    public void SendWithSimulation(System.Action send)
    {
        if (SimPacketLossFraction > 0f && GD.Randf() < SimPacketLossFraction)
            return; // simulated drop

        if (SimLatencySeconds > 0f)
        {
            GetTree().CreateTimer(SimLatencySeconds).Timeout += send;
        }
        else
        {
            send();
        }
    }

    private void OnPeerConnected(long id)
    {
        if (!IsServer)
            return;
        GD.Print($"[Net] Peer connected: {id}");
        Events.Instance.EmitSignal(Events.SignalName.PlayerConnected, id);
    }

    private void OnPeerDisconnected(long id)
    {
        if (!IsServer)
            return;
        GD.Print($"[Net] Peer disconnected: {id}");
        Events.Instance.EmitSignal(Events.SignalName.PlayerDisconnected, id);
    }

    private void OnConnectedToServer() =>
        GD.Print($"[Net] Connected as peer {Multiplayer.GetUniqueId()}.");

    private void OnConnectionFailed()
    {
        if (IsClient)
            Leave("Couldn't reach the room. It may have closed, or your network blocks UDP.");
    }

    private void OnServerDisconnected()
    {
        if (IsClient)
            Leave("Disconnected from the room.");
    }

    private static Dictionary<string, string> ParseCliArgs(string[] rawArgs)
    {
        var result = new Dictionary<string, string>();
        foreach (var arg in rawArgs)
        {
            if (!arg.StartsWith("--")) continue;
            var trimmed = arg[2..];
            var eqIndex = trimmed.IndexOf('=');
            if (eqIndex >= 0)
                result[trimmed[..eqIndex]] = trimmed[(eqIndex + 1)..];
            else
                result[trimmed] = string.Empty;
        }
        return result;
    }
}
