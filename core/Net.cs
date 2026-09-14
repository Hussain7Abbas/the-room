using System.Collections.Generic;
using Godot;
using TheRoom.Config;

namespace TheRoom.Core;

/// <summary>
/// Autoload ("Net" in project.godot). Reads CLI flags and stands up ENet networking:
///   --server            run as a dedicated headless server (also auto-detected when the
///                        display server is "headless", e.g. exported Linux server build)
///   --connect=&lt;ip&gt;       join a server as a client (default 127.0.0.1)
///   --port=&lt;port&gt;        override the default port (60010)
///   --name=&lt;name&gt;        display name to send to the server on connect
///   --bot                run as a headless client driven by simple wander/stab AI instead of
///                        real input (see player/Player.cs) — for reaching 20 players with few humans
///   --sim-latency=&lt;ms&gt;   artificially delay this client's outgoing input/stab RPCs (local testing only)
///   --sim-loss=&lt;0..1&gt;    artificially drop this fraction of this client's outgoing RPCs
///   --character=&lt;id&gt;     which character to play (see abilities/CharacterRegistry.cs); no
///                        character-select UI yet (Phase 3 deferred it), defaults to whichever
///                        character is first in the registry if omitted or unknown
///
/// With none of these flags, the game runs OFFLINE (no MultiplayerPeer at all) so a single
/// person can open the editor and just look at the room/player — useful for quick iteration.
/// </summary>
public partial class Net : Node
{
    public const int DefaultPort = 60010;
    public const int MaxPlayers = 20;

    public static Net Instance { get; private set; } = null!;

    public bool IsServer { get; private set; }
    public bool IsClient { get; private set; }
    public bool IsOffline { get; private set; } = true;
    public bool IsBot { get; private set; }
    public string LocalPlayerName { get; private set; } = "Player";
    public string? ChosenCharacterId { get; private set; }

    /// <summary>Artificial one-way delay (seconds) applied to this client's own outgoing RPCs. 0 = off.</summary>
    public float SimLatencySeconds { get; private set; }
    /// <summary>Fraction [0,1] of this client's own outgoing RPCs to silently drop. 0 = off.</summary>
    public float SimPacketLossFraction { get; private set; }

    private readonly Dictionary<long, string> _connectedPlayerNames = new();

    public override void _Ready()
    {
        Instance = this;

        // Fixed tick rate for deterministic server simulation / hit-rewind (GDD §4, Tuning.ServerTickRateHz).
        Engine.PhysicsTicksPerSecond = TuningService.Instance.ServerTickRateHz;

        // Grammar check every boot (CHARACTER-SPEC.md Part 3 review gate, mechanical half) —
        // a broken ability should fail loudly at startup, not get discovered mid-playtest.
        AbilityValidator.RunAndPrint();

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
            StartClient(string.IsNullOrWhiteSpace(connectHost) ? "127.0.0.1" : connectHost, port);
        }
        else
        {
            GD.Print("[Net] No --server / --connect flag: running OFFLINE (single local player).");
        }
    }

    public void StartServer(int port = DefaultPort)
    {
        var peer = new ENetMultiplayerPeer();
        var err = peer.CreateServer(port, MaxPlayers);
        if (err != Error.Ok)
        {
            GD.PushError($"[Net] Failed to create server on port {port}: {err}");
            return;
        }

        Multiplayer.MultiplayerPeer = peer;
        IsServer = true;
        IsOffline = false;

        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;

        GD.Print($"[Net] Server listening on port {port} (max {MaxPlayers} players).");
    }

    public void StartClient(string host, int port = DefaultPort)
    {
        var peer = new ENetMultiplayerPeer();
        var err = peer.CreateClient(host, port);
        if (err != Error.Ok)
        {
            GD.PushError($"[Net] Failed to connect to {host}:{port}: {err}");
            return;
        }

        Multiplayer.MultiplayerPeer = peer;
        IsClient = true;
        IsOffline = false;

        Multiplayer.ConnectedToServer += () => GD.Print($"[Net] Connected to {host}:{port} as peer {Multiplayer.GetUniqueId()}.");
        Multiplayer.ConnectionFailed += () => GD.PushError($"[Net] Connection to {host}:{port} failed.");
        Multiplayer.ServerDisconnected += () => GD.Print("[Net] Server disconnected.");

        GD.Print($"[Net] Connecting to {host}:{port} ...");
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
        GD.Print($"[Net] Peer connected: {id}");
        Events.Instance.EmitSignal(Events.SignalName.PlayerConnected, id);
    }

    private void OnPeerDisconnected(long id)
    {
        GD.Print($"[Net] Peer disconnected: {id}");
        _connectedPlayerNames.Remove(id);
        Events.Instance.EmitSignal(Events.SignalName.PlayerDisconnected, id);
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
