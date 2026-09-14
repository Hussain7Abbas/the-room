using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheRoom.Core;

/// <summary>
/// Killfeed (Pillar 2: "the victim can always say what killed them") + scoreboard, held with
/// the `scoreboard` action (Tab). Purely a listener on Events.PlayerKilled — no direct Player
/// reference needed, works identically for every client. Grey-box text UI; a styled version is
/// Phase 5 art-pass territory.
/// </summary>
public partial class KillfeedUI : CanvasLayer
{
    private const int MaxKillfeedLines = 5;
    private const double KillfeedLineLifetime = 6.0;

    private VBoxContainer _killfeedList = null!;
    private Control _scoreboardPanel = null!;
    private VBoxContainer _scoreboardList = null!;

    private readonly List<(Label label, double expiresAt)> _killfeedLines = new();
    private readonly Dictionary<long, int> _kills = new();
    private readonly Dictionary<long, int> _deaths = new();

    public override void _Ready()
    {
        _killfeedList = GetNode<VBoxContainer>("KillfeedList");
        _scoreboardPanel = GetNode<Control>("ScoreboardPanel");
        _scoreboardList = GetNode<VBoxContainer>("ScoreboardPanel/ScoreboardList");
        _scoreboardPanel.Visible = false;

        Events.Instance.PlayerKilled += OnPlayerKilled;
    }

    public override void _Process(double delta)
    {
        _scoreboardPanel.Visible = Input.IsActionPressed("scoreboard");
        if (_scoreboardPanel.Visible)
            RebuildScoreboard();

        var now = Time.GetTicksMsec() / 1000.0;
        for (var i = _killfeedLines.Count - 1; i >= 0; i--)
        {
            if (now < _killfeedLines[i].expiresAt)
                continue;

            _killfeedLines[i].label.QueueFree();
            _killfeedLines.RemoveAt(i);
        }
    }

    private void OnPlayerKilled(long killerId, long victimId, string method)
    {
        _kills[killerId] = _kills.GetValueOrDefault(killerId) + 1;
        _deaths[victimId] = _deaths.GetValueOrDefault(victimId) + 1;

        var killerName = killerId == victimId ? null : Main.GetPlayerName(killerId);
        var victimName = Main.GetPlayerName(victimId);
        var line = killerId == victimId || killerName is null
            ? $"{victimName} died ({method})"
            : $"{killerName} killed {victimName} ({method})";

        var label = new Label { Text = line };
        _killfeedList.AddChild(label);
        _killfeedLines.Add((label, Time.GetTicksMsec() / 1000.0 + KillfeedLineLifetime));

        while (_killfeedLines.Count > MaxKillfeedLines)
        {
            _killfeedLines[0].label.QueueFree();
            _killfeedLines.RemoveAt(0);
        }
    }

    private void RebuildScoreboard()
    {
        foreach (var child in _scoreboardList.GetChildren())
            child.QueueFree();

        var allPeers = _kills.Keys.Union(_deaths.Keys).Union(Main.PlayerNames.Keys).Distinct()
            .OrderByDescending(id => _kills.GetValueOrDefault(id));

        foreach (var peerId in allPeers)
        {
            var name = Main.GetPlayerName(peerId);
            var kills = _kills.GetValueOrDefault(peerId);
            var deaths = _deaths.GetValueOrDefault(peerId);
            _scoreboardList.AddChild(new Label { Text = $"{name,-16} K:{kills,-3} D:{deaths}" });
        }

        if (_scoreboardList.GetChildCount() == 0)
            _scoreboardList.AddChild(new Label { Text = "(no players yet)" });
    }
}
