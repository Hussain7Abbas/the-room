using System;
using System.Linq;
using Godot;
using TheRoom.Core;

namespace TheRoom.UI;

/// <summary>
/// The scoreboard held on Tab (or the controller's View/Create), in the main menu's style: a dark
/// card with the display-font title, the match status, and one row per player. Your own row is
/// outlined in the accent red; the leader's rank is gold. Rebuilt a few times a second while shown.
/// </summary>
public partial class ScoreboardView : Control
{
    private const double RefreshSeconds = 0.25;
    private static readonly int[] ColumnWidths = { 36, 0, 70, 48, 48, 70 }; // 0: the name stretches

    private readonly Func<long, int> _kills;
    private readonly Func<long, int> _deaths;
    private readonly Label _status;
    private readonly VBoxContainer _rows = new();
    private double _refreshIn;

    public ScoreboardView(Func<long, int> kills, Func<long, int> deaths)
    {
        _kills = kills;
        _deaths = deaths;
        AnchorRight = 1;
        AnchorBottom = 1;
        MouseFilter = MouseFilterEnum.Ignore;
        Theme = UiTheme.Get();

        var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(center);
        var card = new PanelContainer { CustomMinimumSize = new Vector2(640, 0), MouseFilter = MouseFilterEnum.Ignore };
        var style = new StyleBoxFlat
        {
            BgColor = new Color(UiTheme.Panel, 0.95f),
            BorderColor = UiTheme.Border,
            ShadowColor = new Color(0, 0, 0, 0.45f),
            ShadowSize = 18,
            ContentMarginLeft = 26,
            ContentMarginRight = 26,
            ContentMarginTop = 20,
            ContentMarginBottom = 22,
        };
        style.SetCornerRadiusAll(12);
        style.SetBorderWidthAll(1);
        card.AddThemeStyleboxOverride("panel", style);
        center.AddChild(card);

        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 10);
        card.AddChild(column);

        var header = new HBoxContainer();
        var title = UiTheme.Label("SCOREBOARD", "Heading", fontSize: 30, color: UiTheme.Accent);
        header.AddChild(title);
        header.AddChild(UiTheme.Spacer());
        _status = UiTheme.Label("", "Muted", fontSize: 14);
        _status.SizeFlagsVertical = SizeFlags.ShrinkCenter;
        header.AddChild(_status);
        column.AddChild(header);

        column.AddChild(Row(new[] { "#", "PLAYER", "SCORE", "K", "D", "BOUNTY" }, UiTheme.Muted, 12, null));
        _rows.AddThemeConstantOverride("separation", 4);
        column.AddChild(_rows);
    }

    public override void _Process(double delta)
    {
        if (!Visible)
        {
            _refreshIn = 0;
            return;
        }
        _refreshIn -= delta;
        if (_refreshIn > 0)
            return;
        _refreshIn = RefreshSeconds;
        Rebuild();
    }

    private void Rebuild()
    {
        var match = MatchServer.Instance;
        var seconds = Mathf.CeilToInt(match.MatchTimeRemaining);
        var clock = $"{seconds / 60}:{seconds % 60:00}";
        _status.Text = match.IsLastCall ? $"LAST CALL  ·  {clock} left" : $"First to {match.ScoreTarget}  ·  {clock} left";
        _status.AddThemeColorOverride("font_color", match.IsLastCall ? UiTheme.Accent : UiTheme.Muted);

        foreach (var child in _rows.GetChildren())
            child.QueueFree();

        var me = Multiplayer.HasMultiplayerPeer() ? Multiplayer.GetUniqueId() : 1;
        var peers = Main.PlayerNames.Keys
            .OrderByDescending(match.GetScore)
            .ThenByDescending(_kills)
            .ToList();
        if (peers.Count == 0)
        {
            _rows.AddChild(UiTheme.Label("No players yet", "Muted", fontSize: 14));
            return;
        }

        for (var i = 0; i < peers.Count; i++)
        {
            var id = peers[i];
            var cells = new[]
            {
                (i + 1).ToString(), Main.GetPlayerName(id), match.GetScore(id).ToString(),
                _kills(id).ToString(), _deaths(id).ToString(), match.GetBounty(id).ToString(),
            };
            var rowStyle = new StyleBoxFlat
            {
                BgColor = i % 2 == 0 ? UiTheme.PanelRaised : new Color(UiTheme.PanelRaised, 0.55f),
                BorderColor = UiTheme.Accent,
                ContentMarginLeft = 10,
                ContentMarginRight = 10,
                ContentMarginTop = 6,
                ContentMarginBottom = 6,
            };
            rowStyle.SetCornerRadiusAll(6);
            rowStyle.SetBorderWidthAll(id == me ? 2 : 0);
            var panel = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
            panel.AddThemeStyleboxOverride("panel", rowStyle);
            panel.AddChild(Row(cells, UiTheme.Text, 16, i == 0 ? UiTheme.Gold : null));
            _rows.AddChild(panel);
        }
    }

    private static HBoxContainer Row(string[] cells, Color color, int fontSize, Color? rankColor)
    {
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 8);
        for (var c = 0; c < cells.Length; c++)
        {
            var label = UiTheme.Label(cells[c], fontSize: fontSize, color: c == 0 && rankColor is { } gold ? gold : color);
            if (ColumnWidths[c] == 0)
                label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            else
                label.CustomMinimumSize = new Vector2(ColumnWidths[c], 0);
            label.HorizontalAlignment = c >= 2 ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            label.ClipText = true;
            row.AddChild(label);
        }
        return row;
    }
}
