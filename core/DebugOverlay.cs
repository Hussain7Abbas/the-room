using System.Text;
using Godot;
using TheRoom.Entities;

namespace TheRoom.Core;

/// <summary>
/// Phase 1 network-spike HUD (see plan/phase-1-network-spike.md "Debug overlay" task): ping,
/// tick, and predicted-vs-confirmed stab counts, so a playtest can actually measure the
/// prediction/rewind approach instead of just eyeballing it. Always-on for now — a toggle key
/// can be added later if it gets in the way.
/// </summary>
public partial class DebugOverlay : CanvasLayer
{
    private Label _label = null!;
    private Player? _localPlayer;
    private double _searchTimer;

    public override void _Ready()
    {
        _label = GetNode<Label>("Label");
    }

    public override void _Process(double delta)
    {
        if (_localPlayer is null || !IsInstanceValid(_localPlayer))
        {
            _searchTimer -= delta;
            if (_searchTimer <= 0)
            {
                _localPlayer = FindLocalPlayer();
                _searchTimer = 0.5;
            }
        }

        _label.Text = BuildText();
    }

    private Player? FindLocalPlayer()
    {
        var container = GetNodeOrNull<Node>("/root/Main/PlayersContainer");
        if (container is null)
            return null;

        foreach (var child in container.GetChildren())
        {
            if (child is Player p && p.IsLocallyControlled)
                return p;
        }

        return null;
    }

    private string BuildText()
    {
        var role = Net.Instance.IsServer ? "Server"
            : Net.Instance.IsOffline ? "Offline"
            : Net.Instance.IsBot ? "Bot"
            : "Client";

        var tick = Engine.GetPhysicsFrames();
        var ping = Net.Instance.IsClient ? $"{PingService.Instance.LastRttMs:F0}ms" : "-";

        var text = new StringBuilder();
        text.AppendLine($"Role: {role}   Tick: {tick}   Ping: {ping}");

        if (_localPlayer is not null)
        {
            text.AppendLine(
                $"Stabs — predicted: {_localPlayer.PredictedStabCount}  " +
                $"confirmed: {_localPlayer.ConfirmedStabCount}  " +
                $"last rewind: {_localPlayer.LastRewindMs:F0}ms");
        }

        if (Net.Instance.SimLatencySeconds > 0f || Net.Instance.SimPacketLossFraction > 0f)
        {
            text.AppendLine(
                $"Sim: +{Net.Instance.SimLatencySeconds * 1000f:F0}ms latency, " +
                $"{Net.Instance.SimPacketLossFraction:P0} loss");
        }

        return text.ToString();
    }
}
