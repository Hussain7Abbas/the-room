using System.Collections.Generic;
using System.Linq;
using Godot;
using TheRoom.UI;

namespace TheRoom.Core;

/// <summary>
/// Killfeed (Pillar 2: "the victim can always say what killed them") + scoreboard, held with
/// the `scoreboard` action (Tab), + a top-center announcement banner for Last Call/Bounty/
/// Golden Knife/match results (core/MatchServer.cs). Purely a listener on Events.PlayerKilled
/// and Events.MatchAnnouncement — no direct Player/MatchServer reference needed for the listed
/// events, though the scoreboard does read MatchServer.Instance directly for score/bounty
/// numbers. The clock, scoreboard and results screens are ui/MatchClock, ScoreboardView and
/// ResultsView.
/// </summary>
public partial class KillfeedUI : CanvasLayer
{
    private const int MaxKillfeedLines = 5;
    private const double KillfeedLineLifetime = 6.0;
    private const double AnnouncementLifetime = 4.0;

    private VBoxContainer _killfeedList = null!;
    private ScoreboardView _scoreboard = null!;
    private Label _announcementLabel = null!;
    private ResultsView _results = null!;

    private readonly List<(Label label, double expiresAt)> _killfeedLines = new();
    private readonly Dictionary<long, int> _kills = new();
    private readonly Dictionary<long, int> _deaths = new();
    private double _announcementExpiresAt;

    public override void _Ready()
    {
        _killfeedList = GetNode<VBoxContainer>("KillfeedList");
        _announcementLabel = GetNode<Label>("AnnouncementLabel");
        _announcementLabel.Visible = false;

        // Built in C# with UiTheme (ui/): the match clock + final countdown, the Tab scoreboard
        // and the results podium. Added in this order so the results screen draws on top.
        AddChild(new MatchClock());
        _scoreboard = new ScoreboardView(id => _kills.GetValueOrDefault(id), id => _deaths.GetValueOrDefault(id)) { Visible = false };
        AddChild(_scoreboard);
        _results = new ResultsView { Visible = false };
        AddChild(_results);

        Events.Instance.PlayerKilled += OnPlayerKilled;
        Events.Instance.MatchAnnouncement += OnMatchAnnouncement;
    }

    public override void _Process(double delta)
    {
        _scoreboard.Visible = Input.IsActionPressed("scoreboard") && !MatchServer.Instance.IsResults;

        // Results screen (GDD §4/§7: "over-invested in relative to its build cost", "nobody
        // should leave during it") — shown automatically for the whole results window, no key held.
        _results.Visible = MatchServer.Instance.IsResults;

        var now = Time.GetTicksMsec() / 1000.0;
        for (var i = _killfeedLines.Count - 1; i >= 0; i--)
        {
            if (now < _killfeedLines[i].expiresAt)
                continue;

            _killfeedLines[i].label.QueueFree();
            _killfeedLines.RemoveAt(i);
        }

        // Hidden while the results panel is up — it already says MATCH OVER and lists the MVP, and
        // the banner drew straight across it (seen in real client frames).
        if (_announcementLabel.Visible && (now >= _announcementExpiresAt || MatchServer.Instance.IsResults))
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
        // Golden Knife news in gold, so "X TOOK THE GOLDEN KNIFE" stands out from streaks and kills.
        _announcementLabel.Modulate = text.Contains("GOLDEN KNIFE") ? new Color(1f, 0.8f, 0.25f) : Colors.White;
        _announcementLabel.Visible = true;
        _announcementExpiresAt = Time.GetTicksMsec() / 1000.0 + AnnouncementLifetime;
    }
}
