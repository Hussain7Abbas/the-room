using System.Collections.Generic;
using Godot;

namespace TheRoom.Core;

/// <summary>
/// Autoload ("PingService" in project.godot). Measures round-trip time between each client
/// and the server with a simple echo RPC, since Godot's ENetMultiplayerPeer doesn't expose
/// peer RTT directly. Clients use this to drive the debug overlay; the server uses the
/// reported RTT (halved) as its one-way-latency estimate for hit rewind (CombatServer.cs).
/// </summary>
public partial class PingService : Node
{
    public static PingService Instance { get; private set; } = null!;

    /// <summary>Client-side: this client's most recently measured round-trip time, in ms.</summary>
    public float LastRttMs { get; private set; }

    /// <summary>Server-side: last known RTT (ms) reported by each connected peer.</summary>
    public IReadOnlyDictionary<long, float> PeerRttMs => _peerRttMs;
    private readonly Dictionary<long, float> _peerRttMs = new();

    private double _timeSinceLastPing;

    public override void _Ready()
    {
        Instance = this;
    }

    public override void _Process(double delta)
    {
        if (!Net.Instance.IsClient)
            return;

        _timeSinceLastPing += delta;
        if (_timeSinceLastPing < TuningService.Instance.PingIntervalSeconds)
            return;

        _timeSinceLastPing = 0;
        RpcId(1, nameof(PingRequest), Time.GetTicksMsec());
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void PingRequest(ulong clientSendTimeMs)
    {
        if (!Net.Instance.IsServer)
            return;

        RpcId(Multiplayer.GetRemoteSenderId(), nameof(PongReply), clientSendTimeMs);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void PongReply(ulong originalSendTimeMs)
    {
        if (Multiplayer.GetRemoteSenderId() != 1)
            return; // only trust the server

        LastRttMs = Time.GetTicksMsec() - originalSendTimeMs;
        RpcId(1, nameof(ReportRtt), LastRttMs);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void ReportRtt(float rttMs)
    {
        if (!Net.Instance.IsServer)
            return;

        var peerId = Multiplayer.GetRemoteSenderId();
        if (!_peerRttMs.ContainsKey(peerId))
            GD.Print($"[PingService] First RTT measurement for peer {peerId}: {rttMs:F0}ms.");

        _peerRttMs[peerId] = rttMs;
    }

    /// <summary>Server-side helper: this peer's estimated one-way latency in seconds (RTT/2), or 0 if unknown.</summary>
    public float GetOneWayLatencySeconds(long peerId)
    {
        return _peerRttMs.TryGetValue(peerId, out var rtt) ? rtt / 2000f : 0f;
    }
}
