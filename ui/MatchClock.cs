using Godot;
using TheRoom.Core;

namespace TheRoom.UI;

/// <summary>
/// In-game match time: a small clock at the top centre (accent red during Last Call), and for the
/// last five seconds a huge red 5…1 in the middle of the screen at 50% opacity, popping in with a
/// tick each second. Reads MatchServer.MatchTimeRemaining, which clients count down between the
/// server's clock syncs. Hidden on the results screen.
/// </summary>
public partial class MatchClock : Control
{
    private const int CountdownFrom = 5;
    private static readonly Color CountdownColor = new(0.92f, 0.12f, 0.1f);

    private readonly PanelContainer _clockPanel = new();
    private readonly Label _clock;
    private readonly Label _countdown;
    private int _shownCount = -1;

    public MatchClock()
    {
        AnchorRight = 1;
        AnchorBottom = 1;
        MouseFilter = MouseFilterEnum.Ignore;
        Theme = UiTheme.Get();

        var top = new HBoxContainer
        {
            AnchorRight = 1,
            OffsetTop = 10,
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(top);
        var style = new StyleBoxFlat
        {
            BgColor = new Color(UiTheme.Panel, 0.86f),
            BorderColor = UiTheme.Border,
            ContentMarginLeft = 18,
            ContentMarginRight = 18,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
        };
        style.SetCornerRadiusAll(8);
        style.SetBorderWidthAll(1);
        _clockPanel.AddThemeStyleboxOverride("panel", style);
        _clockPanel.MouseFilter = MouseFilterEnum.Ignore;
        top.AddChild(_clockPanel);
        _clock = UiTheme.Label("0:00", fontSize: 26);
        _clock.AddThemeFontOverride("font", UiTheme.DisplayFont);
        _clock.HorizontalAlignment = HorizontalAlignment.Center;
        _clock.CustomMinimumSize = new Vector2(76, 0);
        _clockPanel.AddChild(_clock);

        var middle = new CenterContainer { AnchorRight = 1, AnchorBottom = 1, MouseFilter = MouseFilterEnum.Ignore };
        AddChild(middle);
        _countdown = UiTheme.Label("", fontSize: 260, color: CountdownColor);
        _countdown.AddThemeFontOverride("font", UiTheme.DisplayFont);
        _countdown.AddThemeColorOverride("font_outline_color", new Color(0.25f, 0f, 0f));
        _countdown.AddThemeConstantOverride("outline_size", 12);
        _countdown.HorizontalAlignment = HorizontalAlignment.Center;
        _countdown.Modulate = new Color(1, 1, 1, 0.5f); // big but see-through: the fight stays visible
        _countdown.Visible = false;
        middle.AddChild(_countdown);
    }

    public override void _Process(double delta)
    {
        var match = MatchServer.Instance;
        if (match.IsResults)
        {
            _clockPanel.Visible = false;
            _countdown.Visible = false;
            _shownCount = -1;
            return;
        }

        var remaining = match.MatchTimeRemaining;
        var seconds = Mathf.CeilToInt(remaining);
        _clockPanel.Visible = true;
        _clock.Text = $"{seconds / 60}:{seconds % 60:00}";
        _clock.AddThemeColorOverride("font_color", match.IsLastCall ? UiTheme.Accent : UiTheme.Text);

        if (remaining <= 0f || seconds > CountdownFrom)
        {
            _countdown.Visible = false;
            _shownCount = -1;
            return;
        }
        if (seconds == _shownCount)
            return;

        _shownCount = seconds;
        _countdown.Text = seconds.ToString();
        _countdown.Visible = true;
        _countdown.PivotOffset = _countdown.Size / 2f;
        _countdown.Scale = Vector2.One * 1.35f;
        var tween = CreateTween();
        tween.TweenProperty(_countdown, "scale", Vector2.One, 0.35f)
            .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        GameAudio.PlayUi("tick", seconds <= 2 ? 0f : -3f);
    }
}
