using System.Collections.Generic;
using System.Linq;
using Godot;

namespace TheRoom.Core;

/// <summary>
/// Killfeed (Pillar 2: "the victim can always say what killed them") + scoreboard, held with
/// the `scoreboard` action (Tab), + a top-center announcement banner for Last Call/Bounty/
/// Golden Knife/match results (core/MatchServer.cs). Purely a listener on Events.PlayerKilled
/// and Events.MatchAnnouncement — no direct Player/MatchServer reference needed for the listed
/// events, though the scoreboard does read MatchServer.Instance directly for score/bounty
/// numbers. Grey-box text UI; a styled version is Phase 5 art-pass territory.
/// </summary>
public partial class KillfeedUI : CanvasLayer
{
    private const int MaxKillfeedLines = 5;
    private const double KillfeedLineLifetime = 6.0;
    private const double AnnouncementLifetime = 4.0;

    private VBoxContainer _killfeedList = null!;
    private Control _scoreboardPanel = null!;
    private VBoxContainer _scoreboardList = null!;
    private Label _announcementLabel = null!;

    private readonly List<(Label label, double expiresAt)> _killfeedLines = new();
    private readonly Dictionary<long, int> _kills = new();
    private readonly Dictionary<long, int> _deaths = new();
    private double _announcementExpiresAt;

    public override void _Ready()
    {
        _killfeedList = GetNode<VBoxContainer>("KillfeedList");
        _scoreboardPanel = GetNode<Control>("ScoreboardPanel");
        _scoreboardList = GetNode<VBoxContainer>("ScoreboardPanel/ScoreboardList");
        _announcementLabel = GetNode<Label>("AnnouncementLabel");
        _scoreboardPanel.Visible = false;
        _announcementLabel.Visible = false;

        Events.Instance.PlayerKilled += OnPlayerKilled;
        Events.Instance.MatchAnnouncement += OnMatchAnnouncement;
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

        if (_announcementLabel.Visible && now >= _announcementExpiresAt)
            _announcementLabel.Visible = false;
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

    private void OnMatchAnnouncement(string text)
    {
        _announcementLabel.Text = text;
        _announcementLabel.Visible = true;
        _announcementExpiresAt = Time.GetTicksMsec() / 1000.0 + AnnouncementLifetime;
    }

    private void RebuildScoreboard()
    {
        foreach (var child in _scoreboardList.GetChildren())
            child.QueueFree();

        var match = MatchServer.Instance;
        var allPeers = _kills.Keys.Union(_deaths.Keys).Union(Main.PlayerNames.Keys).Distinct()
            .OrderByDescending(id => match.GetScore(id));

        var header = match.IsLastCall ? "LAST CALL" : match.IsResults ? "RESULTS" : $"Score to {match.ScoreTarget}";
        _scoreboardList.AddChild(new Label { Text = $"{header} — {match.MatchTimeRemaining:F0}s left" });

        foreach (var peerId in allPeers)
        {
            var name = Main.GetPlayerName(peerId);
            var score = match.GetScore(peerId);
            var bounty = match.GetBounty(peerId);
            var deaths = _deaths.GetValueOrDefault(peerId);
            _scoreboardList.AddChild(new Label { Text = $"{name,-16} Score:{score,-4} Bounty:{bounty,-2} D:{deaths}" });
        }

        if (allPeers.Count() == 0)
            _scoreboardList.AddChild(new Label { Text = "(no players yet)" });
    }
}
