using System.Collections.Generic;
using Godot;
using TheRoom.Core;

namespace TheRoom.UI;

/// <summary>
/// The end-of-match screen, shown for the whole results window. "MATCH OVER" and the countdown to
/// the next match, everyone from 4th down in a list, and under it the podium: 2nd (silver) ·
/// 1st, the MVP (gold, the largest, with a pulsing gold glow) · 3rd (bronze), each smaller than the
/// one before. Built once from MatchServer.LastStandings when it appears.
/// </summary>
public partial class ResultsView : Control
{
    private static readonly Color Silver = new("#c9d1dc");
    private static readonly Color Bronze = new("#cd7f32");

    // Per place (1st, 2nd, 3rd): card size, name font size, rank label.
    private static readonly (Vector2 Size, int NameSize, string Rank, Color Color)[] Places =
    {
        (new Vector2(240, 250), 30, "1ST  ·  MVP", UiTheme.Gold),
        (new Vector2(205, 205), 24, "2ND", Silver),
        (new Vector2(180, 172), 20, "3RD", Bronze),
    };

    private readonly VBoxContainer _column = new();
    private Label? _countdown;
    private StyleBoxFlat? _goldStyle;
    private object? _builtFrom; // the standings list the screen was built from
    private float _time;

    public ResultsView()
    {
        AnchorRight = 1;
        AnchorBottom = 1;
        MouseFilter = MouseFilterEnum.Ignore;
        Theme = UiTheme.Get();
        AddChild(new ColorRect { Color = new Color(0.02f, 0.02f, 0.04f, 0.74f), AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore });
        var center = new CenterContainer { AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(center);
        _column.AddThemeConstantOverride("separation", 16);
        _column.CustomMinimumSize = new Vector2(700, 0);
        center.AddChild(_column);
    }

    public override void _Process(double delta)
    {
        var match = MatchServer.Instance;
        if (!Visible)
        {
            _builtFrom = null;
            return;
        }
        // Online, "match over" can reach a client before the final standings do: rebuild whenever
        // they change, not just when the screen appears.
        if (!ReferenceEquals(match.LastStandings, _builtFrom))
            Build();

        if (_countdown is not null)
            _countdown.Text = $"Next match in {Mathf.CeilToInt(match.ResultsTimeRemaining)} s";
        if (_goldStyle is not null)
        {
            _time += (float)delta;
            var pulse = 0.5f + 0.5f * Mathf.Sin(_time * 3.2f);
            _goldStyle.ShadowSize = (int)(18 + 16 * pulse);
            _goldStyle.ShadowColor = new Color(UiTheme.Gold, 0.35f + 0.3f * pulse);
        }
    }

    private void Build()
    {
        var match = MatchServer.Instance;
        _builtFrom = match.LastStandings;
        foreach (var child in _column.GetChildren())
            child.QueueFree();
        _goldStyle = null;

        var title = UiTheme.Label("MATCH OVER", "Title", fontSize: 58);
        title.HorizontalAlignment = HorizontalAlignment.Center;
        _column.AddChild(title);
        _countdown = UiTheme.Label("", "Muted", fontSize: 15);
        _countdown.HorizontalAlignment = HorizontalAlignment.Center;
        _column.AddChild(_countdown);

        var standings = match.LastStandings;

        // Everyone after the podium, listed above it.
        if (standings.Count > 3)
        {
            var list = new PanelContainer { ThemeTypeVariation = "Card", MouseFilter = MouseFilterEnum.Ignore };
            var rows = new VBoxContainer();
            rows.AddThemeConstantOverride("separation", 4);
            list.AddChild(rows);
            for (var i = 3; i < standings.Count; i++)
            {
                var row = new HBoxContainer();
                var place = UiTheme.Label($"{i + 1}", "Muted", fontSize: 15);
                place.CustomMinimumSize = new Vector2(34, 0);
                row.AddChild(place);
                var name = UiTheme.Label(standings[i].name, fontSize: 15);
                name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                row.AddChild(name);
                row.AddChild(UiTheme.Label($"{standings[i].score} pts", "Muted", fontSize: 15));
                rows.AddChild(row);
            }
            _column.AddChild(list);
        }

        // The podium: 2nd, 1st, 3rd, bottoms aligned like steps.
        var podium = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        podium.AddThemeConstantOverride("separation", 18);
        foreach (var place in new[] { 1, 0, 2 })
        {
            if (place < standings.Count)
                podium.AddChild(PodiumCard(place, standings[place].name, standings[place].score));
        }
        if (standings.Count == 0)
            podium.AddChild(UiTheme.Label("Nobody scored this match.", "Muted", fontSize: 16));
        _column.AddChild(podium);

        if (match.LastAwards.Count > 0)
        {
            var awards = UiTheme.Label(string.Join("   ·   ", match.LastAwards), "Muted", fontSize: 13);
            awards.HorizontalAlignment = HorizontalAlignment.Center;
            awards.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            _column.AddChild(awards);
        }
    }

    private Control PodiumCard(int place, string name, int score)
    {
        var (size, nameSize, rank, color) = Places[place];
        var style = new StyleBoxFlat
        {
            BgColor = UiTheme.Panel.Lerp(color, 0.16f),
            BorderColor = color,
            ShadowColor = new Color(color, place == 0 ? 0.5f : 0.25f),
            ShadowSize = place == 0 ? 24 : 8,
            ContentMarginLeft = 14,
            ContentMarginRight = 14,
            ContentMarginTop = 16,
            ContentMarginBottom = 16,
        };
        style.SetCornerRadiusAll(12);
        style.SetBorderWidthAll(place == 0 ? 3 : 2);
        if (place == 0)
            _goldStyle = style; // pulsed in _Process: the MVP glows

        var card = new PanelContainer
        {
            CustomMinimumSize = size,
            SizeFlagsVertical = SizeFlags.ShrinkEnd,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        card.AddThemeStyleboxOverride("panel", style);
        var inner = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        inner.AddThemeConstantOverride("separation", 6);
        card.AddChild(inner);

        foreach (var label in new List<Label>
        {
            UiTheme.Label(rank, "Heading", fontSize: nameSize - 6, color: color),
            UiTheme.Label(name, "Heading", fontSize: nameSize, color: UiTheme.Text),
            UiTheme.Label($"{score} pts", fontSize: nameSize - 8, color: color),
        })
        {
            label.HorizontalAlignment = HorizontalAlignment.Center;
            label.ClipText = true;
            inner.AddChild(label);
        }
        return card;
    }
}
