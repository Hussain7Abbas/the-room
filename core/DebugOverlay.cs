using System.Text;
using Godot;
using TheRoom.Entities;

namespace TheRoom.Core;

/// <summary>
/// Phase 1 network-spike HUD (see plan/phase-1-network-spike.md "Debug overlay" task): ping,
/// tick, and predicted-vs-confirmed stab counts, so a playtest can actually measure the
/// prediction/rewind approach instead of just eyeballing it. Bottom-left (the player HUD owns the
/// top-left), shown in debug builds only, F3 toggles it.
/// </summary>
public partial class DebugOverlay : CanvasLayer
{
    private Label _label = null!;
    private Player? _localPlayer;
    private double _searchTimer;

    public override void _Ready()
    {
        _label = GetNode<Label>("Label");
        _label.AddThemeFontSizeOverride("font_size", 13);
        Visible = OS.IsDebugBuild();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F3 })
            Visible = !Visible;
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
                $"HP: {_localPlayer.HealthFraction * 100f:F0}%   " +
                $"Attacks — requested: {_localPlayer.AttackRequestCount}  " +
                $"confirmed: {_localPlayer.ConfirmedHitCount}  " +
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
